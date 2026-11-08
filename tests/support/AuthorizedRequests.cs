using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Coordina.Testing;

/// <summary>JSON requests sent as a signed-in user, shared by the API test projects.</summary>
public static class AuthorizedRequests
{
  public static async Task<HttpResponseMessage> SendAuthorizedAsync(
    this HttpClient http,
    HttpMethod method,
    string path,
    string accessToken,
    object? body = null)
  {
    ArgumentNullException.ThrowIfNull(http);

    using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    if (body is not null)
    {
      request.Content = JsonContent.Create(body);
    }

    return await http.SendAsync(request);
  }

  /// <summary>Sends a request that must succeed and returns its JSON body.</summary>
  public static async Task<T> SendAuthorizedForAsync<T>(
    this HttpClient http,
    HttpMethod method,
    string path,
    string accessToken,
    object? body = null)
  {
    using var response = await http.SendAuthorizedAsync(method, path, accessToken, body);

    return await ReadSuccessAsync<T>(response);
  }

  /// <summary>Reads a successful response, or fails the test with the status and body.</summary>
  public static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
  {
    ArgumentNullException.ThrowIfNull(response);

    if (!response.IsSuccessStatusCode)
    {
      var body = await response.Content.ReadAsStringAsync();
      Assert.Fail(
        $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} "
        + $"returned {(int)response.StatusCode}: {body}");
    }

    var payload = await response.Content.ReadFromJsonAsync<T>();
    Assert.NotNull(payload);

    return payload;
  }
}
