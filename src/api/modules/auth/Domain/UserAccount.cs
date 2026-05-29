namespace Coordina.Api.Modules.Auth.Domain;

public sealed record UserAccount(
  Guid Id,
  string Name,
  string Email,
  string NormalizedEmail,
  string PasswordHash,
  DateTimeOffset CreatedAt);
