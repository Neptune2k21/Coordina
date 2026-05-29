namespace Coordina.Api.Modules.Auth.Contracts;

public sealed record AuthResponse(
  string AccessToken,
  DateTimeOffset ExpiresAt,
  CurrentUserResponse User);
