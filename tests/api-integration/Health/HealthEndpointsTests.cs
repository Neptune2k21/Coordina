using System.Net;
using System.Net.Http.Json;
using Coordina.Api.Core.Persistence;
using Coordina.Api.Integration.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coordina.Api.Integration.Tests.Health;

public sealed class HealthEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  // Nothing listens on port 1, so every connection attempt is refused.
  private static readonly PostgresOptions UnreachableDatabase = new()
  {
    Host = "127.0.0.1",
    Port = 1,
    Database = "coordina",
    Username = "coordina",
    Password = "coordina"
  };

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
  public async Task Ready_ReturnsHealthy_WhenDatabaseIsReachable()
  {
    var response = await Api.Http.GetAsync("/health/ready");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task Ready_Returns503_WhenDatabaseIsUnreachable()
  {
    using var client = CreateClientWithoutDatabase();

    var response = await client.GetAsync("/health/ready");

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task Live_ReturnsHealthy_WhenDatabaseIsUnreachable()
  {
    using var client = CreateClientWithoutDatabase();

    var response = await client.GetAsync("/health/live");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
  }

  private HttpClient CreateClientWithoutDatabase() =>
    Factory
      .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        services.Replace(ServiceDescriptor.Singleton(UnreachableDatabase))))
      .CreateClient();

  private sealed record WelcomeResponse(string Message);
}
