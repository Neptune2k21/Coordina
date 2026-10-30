using System.Net;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests;

public sealed class ApiEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  [Fact]
  public async Task Root_ReturnsWelcomeMessage()
  {
    var response = await Api.Http.GetAsync("/");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<WelcomeResponse>();
    Assert.NotNull(payload);
    Assert.Equal("Welcome to the Coordina API.", payload.Message);
  }

  [Fact]
  public async Task HealthLive_ReturnsHealthy()
  {
    var response = await Api.Http.GetAsync("/health/live");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
  }

  private sealed record WelcomeResponse(string Message);
}
