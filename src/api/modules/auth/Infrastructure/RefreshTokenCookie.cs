using Coordina.Api.Modules.Auth.Application;

namespace Coordina.Api.Modules.Auth.Infrastructure;

/// <summary>
/// The refresh token travels only in this cookie: HttpOnly so page scripts
/// cannot read it, Secure, SameSite=Strict so other sites cannot send it, and
/// scoped to /auth so no other request carries it.
/// </summary>
public static class RefreshTokenCookie
{
  public const string Name = "coordina_refresh";
  public const string Path = "/auth";

  /// <summary>
  /// The cookie endpoints also require this header. Browsers only send a
  /// custom header after a CORS preflight, which other origins fail.
  /// </summary>
  public const string CsrfHeader = "X-Requested-With";

  public static string? Read(HttpRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return request.Cookies[Name];
  }

  public static void Write(HttpResponse response, AuthSession session)
  {
    ArgumentNullException.ThrowIfNull(response);
    ArgumentNullException.ThrowIfNull(session);

    response.Cookies.Append(Name, session.RefreshToken, Options(session.RefreshTokenExpiresAt));
  }

  public static void Clear(HttpResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    response.Cookies.Delete(Name, Options(null));
  }

  public static bool HasCsrfHeader(HttpRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return request.Headers.ContainsKey(CsrfHeader);
  }

  private static CookieOptions Options(DateTimeOffset? expires) =>
    new()
    {
      HttpOnly = true,
      Secure = true,
      SameSite = SameSiteMode.Strict,
      Path = Path,
      Expires = expires,
      IsEssential = true
    };
}
