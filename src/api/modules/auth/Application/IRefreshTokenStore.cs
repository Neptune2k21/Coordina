using Coordina.Api.Modules.Auth.Domain;

namespace Coordina.Api.Modules.Auth.Application;

public interface IRefreshTokenStore
{
  Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

  Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

  /// <summary>
  /// Revokes the token unless it already is, in one step, so two requests
  /// cannot both rotate it. Returns <c>false</c> when it was already revoked.
  /// </summary>
  Task<bool> TryRevokeAsync(
    Guid tokenId,
    DateTimeOffset revokedAt,
    Guid? replacedById,
    CancellationToken cancellationToken);

  Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
