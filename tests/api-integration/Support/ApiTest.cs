namespace Coordina.Api.Integration.Tests.Support;

/// <summary>
/// Base class for endpoint tests: one API and one database per test class, and
/// an <see cref="ApiTestClient"/> to drive it. The tests of a class share the
/// database, so each creates the users and workspaces it needs and only looks
/// at those.
/// </summary>
public abstract class ApiTest(ApiTestApplicationFactory factory)
  : IClassFixture<ApiTestApplicationFactory>
{
  protected ApiTestApplicationFactory Factory { get; } = factory;

  protected ApiTestClient Api { get; } = new(factory.CreateClient());

  protected TestDatabase Database => Factory.Database;
}
