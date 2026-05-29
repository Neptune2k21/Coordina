using System.Net.Mail;
using Coordina.Api.Modules.Auth.Contracts;
using Coordina.Api.Modules.Auth.Domain;

namespace Coordina.Api.Modules.Auth.Application;

public sealed class AuthService(
  IUserStore users,
  IPasswordHasher passwordHasher,
  IAccessTokenIssuer accessTokenIssuer) : IAuthService
{
  public async Task<AuthResult<AuthResponse>> RegisterAsync(
    RegisterRequest request,
    CancellationToken cancellationToken)
  {
    var errors = ValidateRegistration(request);

    if (errors.Count > 0)
    {
      return new AuthResult<AuthResponse>(
        AuthResultStatus.ValidationError,
        Errors: errors);
    }

    var normalizedEmail = NormalizeEmail(request.Email);
    var existingUser = await users.FindByEmailAsync(
      normalizedEmail,
      cancellationToken);

    if (existingUser is not null)
    {
      return new AuthResult<AuthResponse>(
        AuthResultStatus.Conflict,
        Message: "An account already exists with this email.");
    }

    var user = new UserAccount(
      Guid.NewGuid(),
      request.Name.Trim(),
      request.Email.Trim(),
      normalizedEmail,
      passwordHasher.Hash(request.Password),
      DateTimeOffset.UtcNow);

    var created = await users.CreateAsync(user, cancellationToken);

    if (!created)
    {
      return new AuthResult<AuthResponse>(
        AuthResultStatus.Conflict,
        Message: "An account already exists with this email.");
    }

    return new AuthResult<AuthResponse>(
      AuthResultStatus.Success,
      CreateAuthResponse(user));
  }

  public async Task<AuthResult<AuthResponse>> LoginAsync(
    LoginRequest request,
    CancellationToken cancellationToken)
  {
    var errors = ValidateLogin(request);

    if (errors.Count > 0)
    {
      return new AuthResult<AuthResponse>(
        AuthResultStatus.ValidationError,
        Errors: errors);
    }

    var user = await users.FindByEmailAsync(
      NormalizeEmail(request.Email),
      cancellationToken);

    if (user is null
      || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
      return new AuthResult<AuthResponse>(AuthResultStatus.Unauthorized);
    }

    return new AuthResult<AuthResponse>(
      AuthResultStatus.Success,
      CreateAuthResponse(user));
  }

  private AuthResponse CreateAuthResponse(UserAccount user)
  {
    var token = accessTokenIssuer.Issue(user);

    return new AuthResponse(
      token.AccessToken,
      token.ExpiresAt,
      new CurrentUserResponse(user.Id.ToString(), user.Email, user.Name));
  }

  private static Dictionary<string, string[]> ValidateRegistration(
    RegisterRequest request)
  {
    var errors = ValidateLogin(new LoginRequest(
      request.Email,
      request.Password));

    if (string.IsNullOrWhiteSpace(request.Name))
    {
      errors[nameof(request.Name)] = ["Name is required."];
    }
    else if (request.Name.Trim().Length < 2)
    {
      errors[nameof(request.Name)] = ["Name must be at least 2 characters."];
    }

    return errors;
  }

  private static Dictionary<string, string[]> ValidateLogin(LoginRequest request)
  {
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
    {
      errors[nameof(request.Email)] = ["A valid email is required."];
    }

    if (string.IsNullOrWhiteSpace(request.Password)
      || request.Password.Length < 8)
    {
      errors[nameof(request.Password)] =
        ["Password must be at least 8 characters."];
    }

    return errors;
  }

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
