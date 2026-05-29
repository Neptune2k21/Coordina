namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class AuthUserEntity
{
  public Guid Id { get; set; }

  public required string Name { get; set; }

  public required string Email { get; set; }

  public required string NormalizedEmail { get; set; }

  public required string PasswordHash { get; set; }

  public DateTimeOffset CreatedAt { get; set; }
}
