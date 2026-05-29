using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests;

public sealed class AuthEndpointsTests(ApiTestApplicationFactory factory)
    : IClassFixture<ApiTestApplicationFactory>
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task Register_ReturnsTokenAndCurrentUser()
  {
    var email = UniqueEmail();

    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "Ada Lovelace",
      email,
      password = "Password123!"
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
    Assert.Equal(email, payload.User.Email);
    Assert.Equal("Ada Lovelace", payload.User.Name);
  }

  [Fact]
  public async Task Login_WithValidCredentials_ReturnsToken()
  {
    var email = UniqueEmail();
    await RegisterAsync(email);

    var response = await _client.PostAsJsonAsync("/auth/login", new
    {
      email,
      password = "Password123!"
    });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
    Assert.Equal(email, payload.User.Email);
  }

  [Fact]
  public async Task Me_WithBearerToken_ReturnsCurrentUser()
  {
    var email = UniqueEmail();
    var auth = await RegisterAsync(email);

    using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
    request.Headers.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      auth.AccessToken);

    var response = await _client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
    Assert.NotNull(payload);
    Assert.Equal(auth.User.Id, payload.Id);
    Assert.Equal(email, payload.Email);
  }

  [Fact]
  public async Task Me_WithoutBearerToken_ReturnsUnauthorized()
  {
    var response = await _client.GetAsync("/auth/me");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Register_WithDuplicateEmail_ReturnsConflict()
  {
    var email = UniqueEmail();
    await RegisterAsync(email);

    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "Ada Lovelace",
      email,
      password = "Password123!"
    });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  [Fact]
  public async Task Register_WithInvalidPayload_ReturnsValidationProblem()
  {
    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "",
      email = "not-an-email",
      password = "short"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  private async Task<AuthResponse> RegisterAsync(string email)
  {
    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "Ada Lovelace",
      email,
      password = "Password123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private static string UniqueEmail() =>
    $"ada-{Guid.NewGuid():N}@coordina.test";

  private sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    CurrentUserResponse User);

  private sealed record CurrentUserResponse(
    string Id,
    string Email,
    string Name);
}
