namespace Coordina.Api.Modules.Auth.Contracts;

public sealed record CurrentUserResponse(
  string Id,
  string Email,
  string Name);
