using Coordina.Api.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Integration.Tests.Persistence;

public sealed class MigrationSmokeTests
{
  [Fact]
  public async Task AllMigrationsApplyToEmptyDatabase()
  {
    await using var database = await TestDatabaseServer.CreateEmptyDatabaseAsync();
    await using var dbContext = database.CreateDbContext();

    await dbContext.Database.MigrateAsync();

    var pending = await dbContext.Database.GetPendingMigrationsAsync();
    var applied = await dbContext.Database.GetAppliedMigrationsAsync();
    Assert.Empty(pending);
    Assert.Equal(dbContext.Database.GetMigrations().Last(), applied.Last());
  }

  [Fact]
  public async Task LegacyTasksTableIsGone()
  {
    await using var database = await TestDatabaseServer.CreateMigratedDatabaseAsync();

    var table = await database.ScalarAsync<string>(
      "SELECT to_regclass('public.tasks')::text");

    Assert.Null(table);
  }
}
