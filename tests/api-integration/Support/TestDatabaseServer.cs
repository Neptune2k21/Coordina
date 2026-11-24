using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Coordina.Api.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Coordina.Api.Integration.Tests.Support;

/// <summary>
/// The disposable PostgreSQL of <c>docker/compose.test.yml</c>. Tests never use
/// its databases directly: each one is a copy of a template that is migrated
/// once, so a new database costs milliseconds.
/// </summary>
public static class TestDatabaseServer
{
  private const string AdminDatabase = "coordina_test";
  private const int TemplateLock = 31;

  private static readonly Lazy<Task<string>> Template = new(PrepareTemplateAsync);

  public static async Task<TestDatabase> CreateMigratedDatabaseAsync() =>
    await CreateDatabaseAsync($"TEMPLATE \"{await Template.Value}\"");

  public static Task<TestDatabase> CreateEmptyDatabaseAsync() =>
    CreateDatabaseAsync(string.Empty);

  /// <summary>A database migrated up to <paramref name="migration"/>, to test the migrations after it.</summary>
  public static async Task<TestDatabase> CreateDatabaseAtMigrationAsync(string migration)
  {
    var database = await CreateEmptyDatabaseAsync();

    await using var dbContext = database.CreateDbContext();
    await dbContext.GetService<IMigrator>().MigrateAsync(migration);

    return database;
  }

  internal static async Task DropDatabaseAsync(string name)
  {
    await using var connection = await OpenAdminConnectionAsync();
    await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
  }

  private static async Task<TestDatabase> CreateDatabaseAsync(string suffix)
  {
    var name = $"coordina_test_{Guid.NewGuid():N}";
    await using var connection = await OpenAdminConnectionAsync();
    await ExecuteAsync(connection, $"CREATE DATABASE \"{name}\" {suffix}");

    return new TestDatabase(OptionsFor(name));
  }

  // The template is named after the migrations' SQL, so later runs reuse it
  // until a migration changes. The lock makes runs that start together build it
  // once, and the rename shows it only when it is complete.
  private static async Task<string> PrepareTemplateAsync()
  {
    var name = $"coordina_template_{MigrationsHash()}";
    var unfinished = $"{name}_unfinished";
    await using var connection = await OpenAdminConnectionAsync();
    await ExecuteAsync(connection, $"SELECT pg_advisory_lock({TemplateLock})");

    if (await ExistsAsync(connection, name))
    {
      return name;
    }

    await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS \"{unfinished}\" WITH (FORCE)");
    await ExecuteAsync(connection, $"CREATE DATABASE \"{unfinished}\"");

    var database = new TestDatabase(OptionsFor(unfinished));
    await using (var dbContext = database.CreateDbContext())
    {
      await dbContext.Database.MigrateAsync();
    }

    database.CloseConnections();
    await ExecuteAsync(connection, $"ALTER DATABASE \"{unfinished}\" RENAME TO \"{name}\"");

    return name;
  }

  private static string MigrationsHash()
  {
    using var dbContext = new TestDatabase(OptionsFor(AdminDatabase)).CreateDbContext();
    var script = dbContext.GetService<IMigrator>().GenerateScript();

    return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(script)))[..16];
  }

  private static PostgresOptions OptionsFor(string database) => new()
  {
    Host = "127.0.0.1",
    Port = int.Parse(
      Environment.GetEnvironmentVariable("POSTGRES_TEST_PORT") ?? "5433",
      CultureInfo.InvariantCulture),
    Database = database,
    Username = "coordina_test",
    Password = "coordina_test"
  };

  // Not pooled: the advisory lock ends with its connection, and nothing stays
  // connected to the server between tests.
  private static async Task<NpgsqlConnection> OpenAdminConnectionAsync()
  {
    var connection = new NpgsqlConnection(
      new NpgsqlConnectionStringBuilder(OptionsFor(AdminDatabase).ToConnectionString())
      {
        Pooling = false
      }.ConnectionString);

    try
    {
      await connection.OpenAsync();
    }
    catch (NpgsqlException exception)
    {
      await connection.DisposeAsync();
      throw new InvalidOperationException(
        $"Integration tests need the test database. Run 'make docker-test-up', then try again. {exception.Message}",
        exception);
    }

    return connection;
  }

  private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string database)
  {
    await using var command = new NpgsqlCommand(
      $"SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = '{database}')",
      connection);

    return await command.ExecuteScalarAsync() is true;
  }

  private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
  {
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
  }
}
