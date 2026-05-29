using Npgsql;

namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class AuthSchemaInitializer(
  NpgsqlDataSource dataSource) : IHostedService
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    await using var command = dataSource.CreateCommand("""
      create table if not exists auth_users (
        id uuid primary key,
        name text not null,
        email text not null,
        normalized_email text not null unique,
        password_hash text not null,
        created_at timestamptz not null
      );
      """);

    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  public Task StopAsync(CancellationToken cancellationToken) =>
    Task.CompletedTask;
}
