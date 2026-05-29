using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Domain;
using Npgsql;

namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class PostgresUserStore(NpgsqlDataSource dataSource) : IUserStore
{
  public async Task<UserAccount?> FindByEmailAsync(
    string normalizedEmail,
    CancellationToken cancellationToken)
  {
    await using var command = dataSource.CreateCommand("""
      select id, name, email, normalized_email, password_hash, created_at
      from auth_users
      where normalized_email = @normalizedEmail
      limit 1;
      """);
    command.Parameters.AddWithValue("normalizedEmail", normalizedEmail);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

    if (!await reader.ReadAsync(cancellationToken))
    {
      return null;
    }

    return new UserAccount(
      reader.GetGuid(0),
      reader.GetString(1),
      reader.GetString(2),
      reader.GetString(3),
      reader.GetString(4),
      reader.GetFieldValue<DateTimeOffset>(5));
  }

  public async Task<bool> CreateAsync(
    UserAccount user,
    CancellationToken cancellationToken)
  {
    await using var command = dataSource.CreateCommand("""
      insert into auth_users (
        id,
        name,
        email,
        normalized_email,
        password_hash,
        created_at
      )
      values (
        @id,
        @name,
        @email,
        @normalizedEmail,
        @passwordHash,
        @createdAt
      )
      on conflict (normalized_email) do nothing;
      """);

    command.Parameters.AddWithValue("id", user.Id);
    command.Parameters.AddWithValue("name", user.Name);
    command.Parameters.AddWithValue("email", user.Email);
    command.Parameters.AddWithValue("normalizedEmail", user.NormalizedEmail);
    command.Parameters.AddWithValue("passwordHash", user.PasswordHash);
    command.Parameters.AddWithValue("createdAt", user.CreatedAt);

    var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

    return affectedRows == 1;
  }
}
