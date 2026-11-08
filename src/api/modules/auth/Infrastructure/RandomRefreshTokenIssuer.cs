using System.Security.Cryptography;
using System.Text;
using Coordina.Api.Modules.Auth.Application;
using Microsoft.AspNetCore.WebUtilities;

namespace Coordina.Api.Modules.Auth.Infrastructure;

/// <summary>
/// 256 random bits per token. Only their SHA-256 is stored: the token itself
/// has enough entropy that a slow hash would add nothing.
/// </summary>
public sealed class RandomRefreshTokenIssuer(AuthOptions options, TimeProvider clock)
  : IRefreshTokenIssuer
{
  public IssuedRefreshToken Issue()
  {
    var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    return new IssuedRefreshToken(token, Hash(token), clock.GetUtcNow().AddDays(options.RefreshTokenDays));
  }

  public string Hash(string token) =>
    Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
