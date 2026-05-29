using Coordina.Api.Modules.Auth.Domain;

namespace Coordina.Api.Modules.Auth.Application;

public interface IUserStore
{
  Task<UserAccount?> FindByEmailAsync(
    string normalizedEmail,
    CancellationToken cancellationToken);

  Task<bool> CreateAsync(
    UserAccount user,
    CancellationToken cancellationToken);
}
