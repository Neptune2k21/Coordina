using System.Net;
using System.Net.Http.Json;
using Coordina.Api.Postgres.Tests.Support;

namespace Coordina.Api.Postgres.Tests.Integration.Auth;

public sealed class RefreshTokenPostgresTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  private const string CookieName = "coordina_refresh";

  [Fact]
  public async Task ConcurrentRefreshes_OfOneToken_RotateItOnce()
  {
    var cookie = await RegisterAsync();

    var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RefreshAsync(cookie)));

    var succeeded = responses.Count(response => response.StatusCode == HttpStatusCode.OK);

    foreach (var response in responses)
    {
      response.Dispose();
    }

    Assert.Equal(1, succeeded);
    Assert.Equal(2, await Database.ScalarAsync<long>("SELECT count(*) FROM auth_refresh_tokens"));
  }

  [Fact]
  public async Task Logout_RevokesTheStoredTokens()
  {
    var cookie = await RegisterAsync();
    using var refreshed = await RefreshAsync(cookie);

    using var request = Request("/auth/logout", CookieOf(refreshed));
    using var loggedOut = await Factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
    Assert.Equal(0, await Database.ScalarAsync<long>(
      "SELECT count(*) FROM auth_refresh_tokens WHERE revoked_at IS NULL"));
  }

  private async Task<string> RegisterAsync()
  {
    using var response = await Factory.CreateClient().PostAsJsonAsync("/auth/register", new
    {
      name = "Session Tester",
      email = $"session-{Guid.NewGuid():N}@coordina.test",
      password = "Password123!"
    });
    response.EnsureSuccessStatusCode();

    return CookieOf(response);
  }

  private async Task<HttpResponseMessage> RefreshAsync(string cookie)
  {
    using var request = Request("/auth/refresh", cookie);

    return await Factory.CreateClient().SendAsync(request);
  }

  private static HttpRequestMessage Request(string path, string cookie)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
    request.Headers.Add("Cookie", $"{CookieName}={cookie}");
    request.Headers.Add("X-Requested-With", "coordina-web");
    return request;
  }

  private static string CookieOf(HttpResponseMessage response)
  {
    var header = Assert.Single(response.Headers.GetValues("Set-Cookie"));

    return header[(CookieName.Length + 1)..header.IndexOf(';', StringComparison.Ordinal)];
  }
}
