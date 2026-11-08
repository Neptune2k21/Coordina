using System.Collections.Concurrent;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Domain;

namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
  private readonly ConcurrentDictionary<Guid, RefreshToken> _tokens = new();
  private readonly Lock _revocation = new();

  public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(token);
    cancellationToken.ThrowIfCancellationRequested();

    _tokens[token.Id] = token;
    return Task.CompletedTask;
  }

  public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return Task.FromResult(_tokens.Values.SingleOrDefault(token => token.TokenHash == tokenHash));
  }

  public Task<bool> TryRevokeAsync(
    Guid tokenId,
    DateTimeOffset revokedAt,
    Guid? replacedById,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_revocation)
    {
      if (!_tokens.TryGetValue(tokenId, out var token) || token.RevokedAt is not null)
      {
        return Task.FromResult(false);
      }

      _tokens[tokenId] = token with { RevokedAt = revokedAt, ReplacedById = replacedById };
      return Task.FromResult(true);
    }
  }

  public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (_revocation)
    {
      foreach (var token in _tokens.Values.Where(token => token.FamilyId == familyId && token.RevokedAt is null))
      {
        _tokens[token.Id] = token with { RevokedAt = revokedAt };
      }
    }

    return Task.CompletedTask;
  }
}
