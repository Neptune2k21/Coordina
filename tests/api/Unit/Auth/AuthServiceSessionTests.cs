using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Contracts;
using Coordina.Api.Modules.Auth.Infrastructure;

namespace Coordina.Api.Tests.Unit.Auth;

/// <summary>Refresh token rotation, reuse detection and sign-out, with a clock the test moves.</summary>
public sealed class AuthServiceSessionTests
{
  private const string Email = "ada@coordina.test";
  private const string Password = "Password123!";

  private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero));
  private readonly AuthService _auth;

  public AuthServiceSessionTests()
  {
    var options = new AuthOptions
    {
      Issuer = "Coordina.Tests",
      Audience = "Coordina.Tests",
      SigningKey = "a-signing-key-that-is-long-enough-for-tests",
      AccessTokenMinutes = 15,
      RefreshTokenDays = 14
    };

    _auth = new AuthService(
      new InMemoryUserStore(),
      new Pbkdf2PasswordHasher(),
      new JwtAccessTokenIssuer(options, _clock),
      new RandomRefreshTokenIssuer(options, _clock),
      new InMemoryRefreshTokenStore(),
      _clock);
  }

  [Fact]
  public async Task Refresh_RotatesTheToken_AndTheNewOneWorks()
  {
    var first = await SignInAsync();

    var second = Success(await _auth.RefreshAsync(first.RefreshToken, CancellationToken.None));
    var third = await _auth.RefreshAsync(second.RefreshToken, CancellationToken.None);

    Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    Assert.True(third.IsSuccess);
  }

  [Fact]
  public async Task ReusingARotatedToken_AfterTheGracePeriod_RevokesTheWholeFamily()
  {
    var first = await SignInAsync();
    var second = Success(await _auth.RefreshAsync(first.RefreshToken, CancellationToken.None));
    _clock.Advance(AuthService.RotationGracePeriod + TimeSpan.FromSeconds(1));

    var reuse = await _auth.RefreshAsync(first.RefreshToken, CancellationToken.None);
    var legitimate = await _auth.RefreshAsync(second.RefreshToken, CancellationToken.None);

    Assert.False(reuse.IsSuccess);
    Assert.False(legitimate.IsSuccess);
  }

  [Fact]
  public async Task ReusingARotatedToken_WithinTheGracePeriod_IsRefusedWithoutRevoking()
  {
    var first = await SignInAsync();
    var second = Success(await _auth.RefreshAsync(first.RefreshToken, CancellationToken.None));

    var race = await _auth.RefreshAsync(first.RefreshToken, CancellationToken.None);
    var winner = await _auth.RefreshAsync(second.RefreshToken, CancellationToken.None);

    Assert.False(race.IsSuccess);
    Assert.True(winner.IsSuccess);
  }

  [Fact]
  public async Task Logout_RevokesEveryTokenOfTheSignIn()
  {
    var first = await SignInAsync();
    var second = Success(await _auth.RefreshAsync(first.RefreshToken, CancellationToken.None));

    await _auth.LogoutAsync(second.RefreshToken, CancellationToken.None);

    Assert.False((await _auth.RefreshAsync(second.RefreshToken, CancellationToken.None)).IsSuccess);
  }

  [Fact]
  public async Task Logout_LeavesOtherSignInsOfTheUserActive()
  {
    var laptop = await SignInAsync();
    var phone = Success(await _auth.LoginAsync(new LoginRequest(Email, Password), CancellationToken.None));

    await _auth.LogoutAsync(laptop.RefreshToken, CancellationToken.None);

    Assert.True((await _auth.RefreshAsync(phone.RefreshToken, CancellationToken.None)).IsSuccess);
  }

  [Fact]
  public async Task Refresh_WithAnExpiredOrUnknownToken_IsRefused()
  {
    var session = await SignInAsync();
    _clock.Advance(TimeSpan.FromDays(15));

    Assert.False((await _auth.RefreshAsync(session.RefreshToken, CancellationToken.None)).IsSuccess);
    Assert.False((await _auth.RefreshAsync("not-a-token", CancellationToken.None)).IsSuccess);
    Assert.False((await _auth.RefreshAsync(null, CancellationToken.None)).IsSuccess);
  }

  private async Task<AuthSession> SignInAsync() =>
    Success(await _auth.RegisterAsync(new RegisterRequest("Ada", Email, Password), CancellationToken.None));

  private static AuthSession Success(Result<AuthSession> result)
  {
    Assert.True(result.IsSuccess);
    return result.Value;
  }

  private sealed class MovableClock(DateTimeOffset start) : TimeProvider
  {
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
  }
}
