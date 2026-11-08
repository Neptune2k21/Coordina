namespace Coordina.Api.Modules.Auth.Domain;

/// <summary>
/// A refresh token as stored: only its hash, so a database leak does not hand
/// out sessions. Tokens of one sign-in share a family; rotating a token
/// revokes it and records the token that replaced it.
/// </summary>
public sealed record RefreshToken(
  Guid Id,
  Guid UserId,
  Guid FamilyId,
  string TokenHash,
  DateTimeOffset CreatedAt,
  DateTimeOffset ExpiresAt,
  DateTimeOffset? RevokedAt,
  Guid? ReplacedById);
