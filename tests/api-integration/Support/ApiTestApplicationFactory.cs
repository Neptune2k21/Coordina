using Coordina.Api.Core.Hosting;
using Coordina.Api.Core.Persistence;
using Coordina.Api.Modules.Boards;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coordina.Api.Integration.Tests.Support;

/// <summary>The API as it runs in production, on a migrated database of its own.</summary>
public sealed class ApiTestApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
  /// <summary>The web app origin the test API allows, as a deployment would configure it.</summary>
  public const string WebAppOrigin = "https://app.coordina.test";

  private TestDatabase? _database;

  public TestDatabase Database => _database
    ?? throw new InvalidOperationException("The test database is not initialized.");

  public async Task InitializeAsync() =>
    _database = await TestDatabaseServer.CreateMigratedDatabaseAsync();

  async Task IAsyncLifetime.DisposeAsync()
  {
    await DisposeAsync();

    if (_database is not null)
    {
      await _database.DisposeAsync();
    }
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment(CoreHostingExtensions.TestingEnvironment);
    builder.UseSetting("Cors:AllowedOrigins:0", WebAppOrigin);
    builder.UseSetting(BoardsModule.SnapshotSetting, "false");

    // Replaced as a service, not as a setting: .env would win over a setting
    // and point the tests at the development database.
    builder.ConfigureTestServices(services =>
      services.Replace(ServiceDescriptor.Singleton(Database.Options)));
  }
}
