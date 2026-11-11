namespace Coordina.Api.Core.Hosting;

/// <summary>
/// The origins the web app is served from, bound from <c>Cors:AllowedOrigins</c>
/// (<c>Cors__AllowedOrigins__0</c>, <c>Cors__AllowedOrigins__1</c>... as
/// environment variables). Only these origins may call the API from a browser
/// and send the refresh cookie.
/// </summary>
public sealed class WebAppCorsOptions
{
  public const string SectionName = "Cors";

  public string[] AllowedOrigins { get; init; } = [];

  public void Validate(IHostEnvironment environment)
  {
    ArgumentNullException.ThrowIfNull(environment);

    // A deployed API without origins would reject every browser call, which
    // looks like an outage rather than a missing setting.
    if (AllowedOrigins.Length == 0
      && !environment.IsDevelopment()
      && !environment.IsEnvironment(CoreHostingExtensions.TestingEnvironment))
    {
      throw new InvalidOperationException(
        "Cors:AllowedOrigins is required outside Development. "
          + "Set Cors__AllowedOrigins__0 to the web app's origin, such as https://app.example.com.");
    }

    foreach (var origin in AllowedOrigins)
    {
      if (!IsOrigin(origin))
      {
        throw new InvalidOperationException(
          $"Cors:AllowedOrigins contains '{origin}', which is not an origin. "
            + "Use scheme://host[:port] with no path or trailing slash, such as https://app.example.com.");
      }
    }
  }

  // Browsers send the Origin header as scheme://host[:port], so anything else,
  // a trailing slash included, would never match and fail silently.
  private static bool IsOrigin(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri)
    && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
    && value == uri.GetLeftPart(UriPartial.Authority);
}
