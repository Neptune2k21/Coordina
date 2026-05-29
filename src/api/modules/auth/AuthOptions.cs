namespace Coordina.Api.Modules.Auth;

public sealed class AuthOptions
{
  public const string SectionName = "Auth";

  public string Issuer { get; init; } = string.Empty;

  public string Audience { get; init; } = string.Empty;

  public string SigningKey { get; init; } = string.Empty;

  public int AccessTokenMinutes { get; init; } = 60;

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

    if (SigningKey.Length < 32)
    {
      throw new InvalidOperationException(
        "Auth signing key must be at least 32 characters long.");
    }
  }
}
