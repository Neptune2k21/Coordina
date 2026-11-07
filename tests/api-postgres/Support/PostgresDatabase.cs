using Coordina.Api.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coordina.Api.Postgres.Tests.Support;

public sealed class PostgresDatabase : IAsyncDisposable
{
  private readonly PostgresContainerFixture _fixture;

  internal PostgresDatabase(PostgresContainerFixture fixture, string name)
  {
    _fixture = fixture;
    Name = name;
    ConnectionString = fixture.ConnectionStringFor(name);
  }

  public string Name { get; }

  public string ConnectionString { get; }

  public CoordinaDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<CoordinaDbContext>()
      .UseNpgsql(ConnectionString)
      .Options;

    return new CoordinaDbContext(options);
  }

  public async Task<NpgsqlConnection> OpenConnectionAsync()
  {
    var connection = new NpgsqlConnection(ConnectionString);
    await connection.OpenAsync();
    return connection;
  }

  public async Task<T?> ScalarAsync<T>(string sql)
  {
    await using var connection = await OpenConnectionAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    var value = await command.ExecuteScalarAsync();

    return value is null or DBNull ? default : (T)value;
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

  public async Task ExecuteAsync(string sql)
  {
    await using var connection = await OpenConnectionAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
  }

  public async ValueTask DisposeAsync()
  {
    NpgsqlConnection.ClearAllPools();
    await _fixture.ExecuteAdminAsync(
      $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
  }
}
