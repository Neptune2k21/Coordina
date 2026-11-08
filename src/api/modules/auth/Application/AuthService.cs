using System.Net.Mail;
using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Auth.Contracts;
using Coordina.Api.Modules.Auth.Domain;
using Coordina.Api.Shared.Validation;

namespace Coordina.Api.Modules.Auth.Application;

public sealed class AuthService(
  IUserStore users,
  IPasswordHasher passwordHasher,
  IAccessTokenIssuer accessTokenIssuer,
  IRefreshTokenIssuer refreshTokenIssuer,
  IRefreshTokenStore refreshTokens,
  TimeProvider clock) : IAuthService
{
  private const string DuplicateEmailMessage =
    "An account already exists with this email.";

  /// <summary>
  /// Two tabs reloading together send the same token; the second one is a
  /// race, not a theft, if it arrives within this window after the rotation.
  /// </summary>
  public static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(10);

  public async Task<Result<AuthSession>> RegisterAsync(
    RegisterRequest request,
    CancellationToken cancellationToken)
  {
    var errors = ValidateLogin(request.Email, request.Password)
      .Required(nameof(request.Name), request.Name, "Name", minLength: 2);

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var normalizedEmail = NormalizeEmail(request.Email);
    var existingUser = await users.FindByEmailAsync(
      normalizedEmail,
      cancellationToken);

    if (existingUser is not null)
    {
      return Error.Conflict(DuplicateEmailMessage);
    }

    var user = new UserAccount(
      Guid.NewGuid(),
      request.Name.Trim(),
      request.Email.Trim(),
      normalizedEmail,
      passwordHasher.Hash(request.Password),
      clock.GetUtcNow());

    var created = await users.CreateAsync(user, cancellationToken);

    return created
      ? Result.Success(await StartSessionAsync(user, Guid.NewGuid(), cancellationToken))
      : Error.Conflict(DuplicateEmailMessage);
  }

  public async Task<Result<AuthSession>> LoginAsync(
    LoginRequest request,
    CancellationToken cancellationToken)
  {
    var errors = ValidateLogin(request.Email, request.Password);

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var user = await users.FindByEmailAsync(
      NormalizeEmail(request.Email),
      cancellationToken);

    if (user is null
      || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
      return Error.Unauthorized();
    }

    return Result.Success(await StartSessionAsync(user, Guid.NewGuid(), cancellationToken));
  }

  public async Task<Result<AuthSession>> RefreshAsync(
    string? refreshToken,
    CancellationToken cancellationToken)
  {
    var stored = string.IsNullOrEmpty(refreshToken)
      ? null
      : await refreshTokens.FindByHashAsync(refreshTokenIssuer.Hash(refreshToken), cancellationToken);
    var now = clock.GetUtcNow();

    if (stored is null || stored.ExpiresAt <= now)
    {
      return Error.Unauthorized();
    }

    if (stored.RevokedAt is { } revokedAt)
    {
      var isRecentRotation = stored.ReplacedById is not null && now - revokedAt < RotationGracePeriod;

      if (!isRecentRotation)
      {
        await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
      }

      return Error.Unauthorized();
    }

    var replacementId = Guid.NewGuid();

    if (!await refreshTokens.TryRevokeAsync(stored.Id, now, replacementId, cancellationToken))
    {
      return Error.Unauthorized();
    }

    var user = await users.FindByIdAsync(stored.UserId, cancellationToken);

    return user is null
      ? Error.Unauthorized()
      : Result.Success(await StartSessionAsync(user, stored.FamilyId, cancellationToken, replacementId));
  }

  public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
  {
    if (string.IsNullOrEmpty(refreshToken))
    {
      return;
    }

    var stored = await refreshTokens.FindByHashAsync(refreshTokenIssuer.Hash(refreshToken), cancellationToken);

    if (stored is not null)
    {
      await refreshTokens.RevokeFamilyAsync(stored.FamilyId, clock.GetUtcNow(), cancellationToken);
    }
  }

  private async Task<AuthSession> StartSessionAsync(
    UserAccount user,
    Guid familyId,
    CancellationToken cancellationToken,
    Guid? refreshTokenId = null)
  {
    var access = accessTokenIssuer.Issue(user);
    var refresh = refreshTokenIssuer.Issue();

    await refreshTokens.AddAsync(
      new RefreshToken(
        refreshTokenId ?? Guid.NewGuid(),
        user.Id,
        familyId,
        refresh.Hash,
        clock.GetUtcNow(),
        refresh.ExpiresAt,
        null,
        null),
      cancellationToken);

    return new AuthSession(
      new AuthResponse(
        access.AccessToken,
        access.ExpiresAt,
        new CurrentUserResponse(user.Id.ToString(), user.Email, user.Name)),
      refresh.Token,
      refresh.ExpiresAt);
  }

  private static ValidationErrors ValidateLogin(string? email, string? password) =>
    new ValidationErrors()
      .AddIf(
        string.IsNullOrWhiteSpace(email) || !IsValidEmail(email),
        nameof(LoginRequest.Email),
        "A valid email is required.")
      .AddIf(
        string.IsNullOrWhiteSpace(password) || password.Length < 8,
        nameof(LoginRequest.Password),
        "Password must be at least 8 characters.");

  private static bool IsValidEmail(string email)
  {
    try
    {
      _ = new MailAddress(email);
      return true;
    }
    catch (FormatException)
    {
      return false;
    }
  }

  private static string NormalizeEmail(string email) =>
    email.Trim().ToUpperInvariant();
}
