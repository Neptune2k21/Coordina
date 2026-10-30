using System.Net;
using Coordina.Api.Postgres.Tests.Support;

namespace Coordina.Api.Postgres.Tests.Integration.Health;

public sealed class HealthPostgresTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  // Nothing listens on port 1, so every connection attempt is refused.
  private const string UnreachableDatabase =
    "Host=127.0.0.1;Port=1;Database=coordina;Username=coordina;Password=coordina;Timeout=2";

  [Fact]
  public async Task Ready_ReturnsHealthy_WhenDatabaseIsReachable()
  {
    using var client = Factory.CreateClient();

    var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task Ready_Returns503_WhenDatabaseIsUnreachable()
  {
    await using var factory = new PostgresApiFactory(UnreachableDatabase);
    using var client = factory.CreateClient();

    var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task Live_ReturnsHealthy_WhenDatabaseIsUnreachable()
  {
    await using var factory = new PostgresApiFactory(UnreachableDatabase);
    using var client = factory.CreateClient();

    var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }
}
