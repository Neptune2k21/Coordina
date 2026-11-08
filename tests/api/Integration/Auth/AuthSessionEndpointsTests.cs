using System.Net;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Auth;

/// <summary>The refresh cookie: how it is set, rotated, protected and revoked over HTTP.</summary>
public sealed class AuthSessionEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private const string CookieName = "coordina_refresh";

  private readonly string _email = ApiTestClient.UniqueEmail("session");

  [Fact]
  public async Task Register_SetsAnHttpOnlySecureStrictCookieScopedToAuth()
  {
    using var response = await RegisterAsync();

    var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
    Assert.StartsWith($"{CookieName}=", cookie, StringComparison.Ordinal);
    Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("path=/auth", cookie, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task Refresh_RotatesTheCookie_AndReturnsAWorkingAccessToken()
  {
    using var registered = await RegisterAsync();
    var first = CookieOf(registered);

    using var refreshed = await SendWithCookieAsync("/auth/refresh", first);
    var session = await refreshed.Content.ReadFromJsonAsync<AuthResponse>();
    using var me = await Api.SendAsync(HttpMethod.Get, "/auth/me", session!.AccessToken);

    Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    Assert.NotEqual(first, CookieOf(refreshed));
    Assert.Equal(HttpStatusCode.OK, me.StatusCode);
  }

  [Fact]
  public async Task Refresh_WithoutTheCsrfHeader_IsForbidden()
  {
    using var registered = await RegisterAsync();

    using var refreshed = await SendWithCookieAsync("/auth/refresh", CookieOf(registered), withCsrfHeader: false);

    Assert.Equal(HttpStatusCode.Forbidden, refreshed.StatusCode);
  }

  [Fact]
  public async Task Refresh_WithoutACookie_IsUnauthorizedAndClearsIt()
  {
    using var refreshed = await SendWithCookieAsync("/auth/refresh", cookie: null);

    Assert.Equal(HttpStatusCode.Unauthorized, refreshed.StatusCode);
    Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(refreshed.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task Logout_ClearsTheCookie_AndTheTokenNoLongerRefreshes()
  {
    using var registered = await RegisterAsync();
    var cookie = CookieOf(registered);

    using var loggedOut = await SendWithCookieAsync("/auth/logout", cookie);
    using var refreshed = await SendWithCookieAsync("/auth/refresh", cookie);

    Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
    Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(loggedOut.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
    Assert.Equal(HttpStatusCode.Unauthorized, refreshed.StatusCode);
  }

  [Fact]
  public async Task Login_WithAWrongPassword_SetsNoCookie()
  {
    using var registered = await RegisterAsync();

    using var response = await Api.Http.PostAsJsonAsync("/auth/login", new { email = _email, password = "WrongPassword1!" });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.False(response.Headers.Contains("Set-Cookie"));
  }

  private Task<HttpResponseMessage> RegisterAsync() =>
    Api.Http.PostAsJsonAsync("/auth/register", new { name = "Ada", email = _email, password = ApiTestClient.Password });

  private Task<HttpResponseMessage> SendWithCookieAsync(string path, string? cookie, bool withCsrfHeader = true)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));

    if (cookie is not null)
    {
      request.Headers.Add("Cookie", $"{CookieName}={cookie}");
    }

    if (withCsrfHeader)
    {
      request.Headers.Add("X-Requested-With", "coordina-web");
    }

    return Api.Http.SendAsync(request);
  }

  private static string CookieOf(HttpResponseMessage response)
  {
    var header = Assert.Single(response.Headers.GetValues("Set-Cookie"));

    return header[(CookieName.Length + 1)..header.IndexOf(';', StringComparison.Ordinal)];
  }
}
