using Coordina.Api.Core.Persistence;
using Coordina.Api.Core.Results;
using Coordina.Api.Integration.Tests.Support;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Contracts;
using Coordina.Api.Modules.Auth.Infrastructure;

namespace Coordina.Api.Integration.Tests.Auth;

/// <summary>Refresh token rotation, reuse detection and sign-out, with a clock the test moves.</summary>
public sealed class RefreshTokenRotationTests(ApiTestApplicationFactory factory)
  : IClassFixture<ApiTestApplicationFactory>
{
  private static readonly AuthOptions Options = new()
  {
    Issuer = "Coordina.Tests",
    Audience = "Coordina.Tests",
    SigningKey = "a-signing-key-that-is-long-enough-for-tests",
    AccessTokenMinutes = 15,
    RefreshTokenDays = 14
  };

  private readonly string _email = ApiTestClient.UniqueEmail("session");
  private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero));

  [Fact]
  public async Task Refresh_RotatesTheToken_AndTheNewOneWorks()
  {
    var first = await SignInAsync();

    var second = Success(await RefreshAsync(first.RefreshToken));
    var third = await RefreshAsync(second.RefreshToken);

    Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    Assert.True(third.IsSuccess);
  }

  [Fact]
  public async Task ReusingARotatedToken_AfterTheGracePeriod_RevokesTheWholeFamily()
  {
    var first = await SignInAsync();
    var second = Success(await RefreshAsync(first.RefreshToken));
    _clock.Advance(AuthService.RotationGracePeriod + TimeSpan.FromSeconds(1));

    var reuse = await RefreshAsync(first.RefreshToken);
    var legitimate = await RefreshAsync(second.RefreshToken);

    Assert.False(reuse.IsSuccess);
    Assert.False(legitimate.IsSuccess);
  }

  [Fact]
  public async Task ReusingARotatedToken_WithinTheGracePeriod_IsRefusedWithoutRevoking()
  {
    var first = await SignInAsync();
    var second = Success(await RefreshAsync(first.RefreshToken));

    var race = await RefreshAsync(first.RefreshToken);
    var winner = await RefreshAsync(second.RefreshToken);

    Assert.False(race.IsSuccess);
    Assert.True(winner.IsSuccess);
  }

  [Fact]
  public async Task Logout_RevokesEveryTokenOfTheSignIn()
  {
    var first = await SignInAsync();
    var second = Success(await RefreshAsync(first.RefreshToken));

    await LogoutAsync(second.RefreshToken);

    Assert.False((await RefreshAsync(second.RefreshToken)).IsSuccess);
  }

  [Fact]
  public async Task Logout_LeavesOtherSignInsOfTheUserActive()
  {
    var laptop = await SignInAsync();
    var phone = Success(await LoginAsync());

    await LogoutAsync(laptop.RefreshToken);

    Assert.True((await RefreshAsync(phone.RefreshToken)).IsSuccess);
  }

  [Fact]
  public async Task Refresh_WithAnExpiredOrUnknownToken_IsRefused()
  {
    var session = await SignInAsync();
    _clock.Advance(TimeSpan.FromDays(15));

    Assert.False((await RefreshAsync(session.RefreshToken)).IsSuccess);
    Assert.False((await RefreshAsync("not-a-token")).IsSuccess);
    Assert.False((await RefreshAsync(null)).IsSuccess);
  }

  private async Task<AuthSession> SignInAsync() =>
    Success(await SendAsync(auth => auth.RegisterAsync(
      new RegisterRequest("Ada", _email, ApiTestClient.Password), CancellationToken.None)));

  private Task<Result<AuthSession>> LoginAsync() =>
    SendAsync(auth => auth.LoginAsync(
      new LoginRequest(_email, ApiTestClient.Password), CancellationToken.None));

  private Task<Result<AuthSession>> RefreshAsync(string? token) =>
    SendAsync(auth => auth.RefreshAsync(token, CancellationToken.None));

  private Task<bool> LogoutAsync(string token) =>
    SendAsync(async auth =>
    {
      await auth.LogoutAsync(token, CancellationToken.None);
      return true;
    });

  // Each call gets its own context, as each request does.
  private async Task<T> SendAsync<T>(Func<AuthService, Task<T>> call)
  {
    await using var dbContext = factory.Database.CreateDbContext();

    return await call(CreateService(dbContext));
  }

  private AuthService CreateService(CoordinaDbContext dbContext) => new(
    new PostgresUserStore(dbContext),
    new Pbkdf2PasswordHasher(),
    new JwtAccessTokenIssuer(Options, _clock),
    new RandomRefreshTokenIssuer(Options, _clock),
    new PostgresRefreshTokenStore(dbContext),
    _clock);

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
