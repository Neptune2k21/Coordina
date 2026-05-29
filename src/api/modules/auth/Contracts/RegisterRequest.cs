namespace Coordina.Api.Modules.Auth.Contracts;

public sealed record RegisterRequest(
  string Name,
  string Email,
  string Password);
