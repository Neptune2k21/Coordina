using System.Net.Http.Json;
using Coordina.Testing;

namespace Coordina.Api.Postgres.Tests.Support;

public sealed class ApiClient(HttpClient client)
{
  public async Task<TestUser> RegisterAsync()
  {
    var response = await client.PostAsJsonAsync("/auth/register", new
    {
      name = "Postgres Tester",
      email = $"postgres-{Guid.NewGuid():N}@coordina.test",
      password = "Password123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
    Assert.NotNull(payload);

    return new TestUser(payload.AccessToken, payload.User.Id);
  }

  public Task<HttpResponseMessage> SendAsync(
    HttpMethod method,
    string path,
    string accessToken,
    object? body = null) =>
    client.SendAuthorizedAsync(method, path, accessToken, body);

  public Task<T> SendForAsync<T>(
    HttpMethod method,
    string path,
    string accessToken,
    object? body = null) =>
    client.SendAuthorizedForAsync<T>(method, path, accessToken, body);

  private sealed record AuthResponse(string AccessToken, UserResponse User);

  private sealed record UserResponse(Guid Id);
}

public sealed record TestUser(string AccessToken, Guid Id);
