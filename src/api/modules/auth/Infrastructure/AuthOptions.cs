namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class AuthOptions
{
  public const string SectionName = "Auth";

  public string Issuer { get; init; } = string.Empty;

  public string Audience { get; init; } = string.Empty;

  public string SigningKey { get; init; } = string.Empty;

  /// <summary>Short on purpose: the refresh cookie renews it, and a stolen one expires soon.</summary>
  public int AccessTokenMinutes { get; init; } = 15;

  public int RefreshTokenDays { get; init; } = 14;

  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Issuer))
    {
      throw new InvalidOperationException("Auth issuer is required.");
    }

    if (string.IsNullOrWhiteSpace(Audience))
    {
      throw new InvalidOperationException("Auth audience is required.");
    }

    if (AccessTokenMinutes < 1 || RefreshTokenDays < 1)
    {
      throw new InvalidOperationException(
        "Auth token lifetimes must be at least one minute and one day.");
    }

    if (SigningKey.Length < 32)
    {
      throw new InvalidOperationException(
        "Auth signing key must be at least 32 characters long.");
    }
  }
}
