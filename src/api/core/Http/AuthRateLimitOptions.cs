namespace Coordina.Api.Core.Http;

/// <summary>
/// How many sign-in and sign-up attempts one client IP may make per window,
/// bound from <c>RateLimiting:Auth</c> (<c>RateLimiting__Auth__PermitLimit</c>,
/// <c>RateLimiting__Auth__WindowSeconds</c> as environment variables).
/// </summary>
public sealed class AuthRateLimitOptions
{
  public const string SectionName = "RateLimiting:Auth";

  /// <summary>Enough for a person who mistypes a password, far too few for guessing one.</summary>
  public int PermitLimit { get; init; } = 10;

  public int WindowSeconds { get; init; } = 60;

  public void Validate()
  {
    if (PermitLimit < 1 || WindowSeconds < 1)
    {
      throw new InvalidOperationException(
        "RateLimiting:Auth needs a PermitLimit and a WindowSeconds of at least 1.");
    }
  }
}
