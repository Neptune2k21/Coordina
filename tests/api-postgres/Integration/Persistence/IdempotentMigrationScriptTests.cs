using Coordina.Api.Postgres.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coordina.Api.Postgres.Tests.Integration.Persistence;

/// <summary>
/// A deployment can apply this script instead of running the migrations. It
/// wraps each migration in a DO block, where raw SQL without a final semicolon
/// is a syntax error that <c>database update</c> never reveals.
/// </summary>
public sealed class IdempotentMigrationScriptTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  private const string SchemaQuery =
    """
    SELECT string_agg(
      table_name || '.' || column_name || ':' || data_type || ':' || is_nullable,
      ',' ORDER BY table_name, column_name)
    FROM information_schema.columns
    WHERE table_schema = 'public'
    """;

  [Fact]
  public async Task Script_AppliedTwiceOnAnEmptyDatabase_MatchesTheMigrations()
  {
    await using var empty = await Postgres.CreateEmptyDatabaseAsync();
    await using var dbContext = empty.CreateDbContext();
    var script = dbContext.GetService<IMigrator>().GenerateScript(
      options: MigrationsSqlGenerationOptions.Idempotent);
    var migrationCount = dbContext.Database.GetMigrations().Count();

    await empty.ExecuteAsync(script);

    var schema = await empty.ScalarAsync<string>(SchemaQuery);

    Assert.Equal(await Database.ScalarAsync<string>(SchemaQuery), schema);
    Assert.Equal(migrationCount, await CountAppliedAsync(empty));

    await empty.ExecuteAsync(script);

    Assert.Equal(schema, await empty.ScalarAsync<string>(SchemaQuery));
    Assert.Equal(migrationCount, await CountAppliedAsync(empty));
  }

  private static async Task<int> CountAppliedAsync(PostgresDatabase database) =>
    (int)await database.ScalarAsync<long>(
      """SELECT count(*) FROM "__EFMigrationsHistory" """);
}
