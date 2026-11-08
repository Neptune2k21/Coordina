using Coordina.Api.Modules.Auth.Contracts;

namespace Coordina.Api.Modules.Auth.Application;

/// <summary>
/// What signing in or refreshing produces: the response body, with the
/// short-lived access token, and the refresh token the endpoint puts in a cookie.
/// </summary>
public sealed record AuthSession(
  AuthResponse Response,
  string RefreshToken,
  DateTimeOffset RefreshTokenExpiresAt);
