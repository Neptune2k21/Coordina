using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Security;

public sealed class CorsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  [Fact]
  public async Task Preflight_FromAConfiguredOrigin_AllowsTheCallWithCredentials()
  {
    var response = await Api.Http.SendAsync(Preflight(ApiTestApplicationFactory.WebAppOrigin));

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(
      ApiTestApplicationFactory.WebAppOrigin,
      Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
  }

  [Fact]
  public async Task Request_FromAConfiguredOrigin_ReceivesTheCorsHeaders()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/");
    request.Headers.Add("Origin", ApiTestApplicationFactory.WebAppOrigin);

    var response = await Api.Http.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(
      ApiTestApplicationFactory.WebAppOrigin,
      Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
  }

  [Theory]
  [InlineData("https://evil.example")]
  [InlineData("http://localhost:5173")]
  public async Task Preflight_FromAnotherOrigin_GetsNoCorsHeaders(string origin)
  {
    var response = await Api.Http.SendAsync(Preflight(origin));

    Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
  }

  private static HttpRequestMessage Preflight(string origin)
  {
    var request = new HttpRequestMessage(HttpMethod.Options, "/auth/login");
    request.Headers.Add("Origin", origin);
    request.Headers.Add("Access-Control-Request-Method", "POST");
    request.Headers.Add("Access-Control-Request-Headers", "content-type");

    return request;
  }
}
