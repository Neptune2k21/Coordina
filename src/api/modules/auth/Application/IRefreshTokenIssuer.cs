namespace Coordina.Api.Modules.Auth.Application;

public interface IRefreshTokenIssuer
{
  IssuedRefreshToken Issue();

  /// <summary>The hash a token is stored and looked up by.</summary>
  string Hash(string token);
}

/// <param name="Token">The secret given to the browser, never stored.</param>
public sealed record IssuedRefreshToken(
  string Token,
  string Hash,
  DateTimeOffset ExpiresAt);
