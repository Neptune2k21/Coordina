using Coordina.Api.Core.Persistence;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class PostgresRefreshTokenStore(CoordinaDbContext dbContext) : IRefreshTokenStore
{
  private DbSet<RefreshTokenEntity> Tokens => dbContext.Set<RefreshTokenEntity>();

  public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(token);

    Tokens.Add(new RefreshTokenEntity
    {
      Id = token.Id,
      UserId = token.UserId,
      FamilyId = token.FamilyId,
      TokenHash = token.TokenHash,
      CreatedAt = token.CreatedAt,
      ExpiresAt = token.ExpiresAt,
      RevokedAt = token.RevokedAt,
      ReplacedById = token.ReplacedById
    });

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
  {
    var token = await Tokens
      .AsNoTracking()
      .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

    return token is null
      ? null
      : new RefreshToken(
        token.Id,
        token.UserId,
        token.FamilyId,
        token.TokenHash,
        token.CreatedAt,
        token.ExpiresAt,
        token.RevokedAt,
        token.ReplacedById);
  }

  public async Task<bool> TryRevokeAsync(
    Guid tokenId,
    DateTimeOffset revokedAt,
    Guid? replacedById,
    CancellationToken cancellationToken) =>
    await Tokens
      .Where(token => token.Id == tokenId && token.RevokedAt == null)
      .ExecuteUpdateAsync(
        setters => setters
          .SetProperty(token => token.RevokedAt, revokedAt)
          .SetProperty(token => token.ReplacedById, replacedById),
        cancellationToken) == 1;

  public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
    Tokens
      .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
      .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, revokedAt), cancellationToken);
}
