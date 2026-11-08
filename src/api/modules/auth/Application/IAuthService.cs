using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Auth.Contracts;

namespace Coordina.Api.Modules.Auth.Application;

public interface IAuthService
{
  Task<Result<AuthSession>> RegisterAsync(
    RegisterRequest request,
    CancellationToken cancellationToken);

  Task<Result<AuthSession>> LoginAsync(
    LoginRequest request,
    CancellationToken cancellationToken);

  /// <summary>
  /// Exchanges a refresh token for a new session in the same family. A token
  /// that was already used revokes the whole family: someone else has it.
  /// </summary>
  Task<Result<AuthSession>> RefreshAsync(
    string? refreshToken,
    CancellationToken cancellationToken);

  /// <summary>Revokes the sign-in the refresh token belongs to, if any.</summary>
  Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}
