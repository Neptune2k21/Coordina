using Coordina.Api.Modules.Auth.Domain;

namespace Coordina.Api.Modules.Auth.Application;

public interface IAccessTokenIssuer
{
  IssuedAccessToken Issue(UserAccount user);
}

public sealed record IssuedAccessToken(
  string AccessToken,
  DateTimeOffset ExpiresAt);
