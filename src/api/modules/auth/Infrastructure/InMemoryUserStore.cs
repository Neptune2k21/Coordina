using System.Collections.Concurrent;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Domain;

namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class InMemoryUserStore : IUserStore
{
  private readonly ConcurrentDictionary<string, UserAccount> _users = new();

  public Task<UserAccount?> FindByEmailAsync(
    string normalizedEmail,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    _users.TryGetValue(normalizedEmail, out var user);
    return Task.FromResult(user);
  }

  public Task<bool> CreateAsync(
    UserAccount user,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return Task.FromResult(_users.TryAdd(user.NormalizedEmail, user));
  }
}
