using Coordina.Api.Modules.Auth.Contracts;

namespace Coordina.Api.Modules.Auth.Application;

public interface IAuthService
{
  Task<AuthResult<AuthResponse>> RegisterAsync(
    RegisterRequest request,
    CancellationToken cancellationToken);

  Task<AuthResult<AuthResponse>> LoginAsync(
    LoginRequest request,
    CancellationToken cancellationToken);
}
