using Coordina.Api.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coordina.Api.Integration.Tests.Support;

/// <summary>A database of its own on the test server, dropped when disposed.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
  internal TestDatabase(PostgresOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    Options = options;
  }

  public PostgresOptions Options { get; }

  public CoordinaDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<CoordinaDbContext>()
      .UseNpgsql(Options.ToConnectionString())
      .Options;

    return new CoordinaDbContext(options);
  }

  public async Task<T?> ScalarAsync<T>(string sql)
  {
    await using var connection = await OpenConnectionAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    var value = await command.ExecuteScalarAsync();

    return value is null or DBNull ? default : (T)value;
  }

  public async Task ExecuteAsync(string sql)
  {
    await using var connection = await OpenConnectionAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
  }

  /// <summary>
  /// Inserts a workspace and its projects with SQL, for migration tests that
  /// run against an older schema the API's stores no longer match.
  /// </summary>
  public Task InsertWorkspaceWithProjectsAsync(Guid workspaceId, params Guid[] projectIds) =>
    ExecuteAsync($"""
      INSERT INTO workspaces (id, name, created_at)
      VALUES ('{workspaceId}', 'Legacy', now());
      INSERT INTO projects (id, workspace_id, name, project_owner_id, status, created_at, updated_at)
      VALUES {string.Join(", ", projectIds.Select(projectId =>
        $"('{projectId}', '{workspaceId}', 'Legacy', gen_random_uuid(), 'ACTIVE', now(), now())"))};
      """);

  public async ValueTask DisposeAsync()
  {
    CloseConnections();
    await TestDatabaseServer.DropDatabaseAsync(Options.Database);
  }

  internal void CloseConnections()
  {
    using var connection = new NpgsqlConnection(Options.ToConnectionString());
    NpgsqlConnection.ClearPool(connection);
  }

  private async Task<NpgsqlConnection> OpenConnectionAsync()
  {
    var connection = new NpgsqlConnection(Options.ToConnectionString());
    await connection.OpenAsync();

    return connection;
  }
}
