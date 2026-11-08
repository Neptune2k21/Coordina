using System.Security.Claims;
using Coordina.Api.Core.Http;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Contracts;
using Coordina.Api.Modules.Auth.Infrastructure;

namespace Coordina.Api.Modules.Auth;

public static class AuthEndpoints
{
  public static IEndpointRouteBuilder MapAuthEndpoints(
    this IEndpointRouteBuilder app)
  {
    var group = app.MapApiGroup("/auth", "Auth");

    group.MapPost("/register", Register)
      .Produces<AuthResponse>(StatusCodes.Status201Created)
      .AllowAnonymous()
      .WithSummary("Create a workspace account")
      .WithDescription("Creates a user account, returns an access token and sets the refresh cookie.");
    group.MapPost("/login", Login)
      .Produces<AuthResponse>(StatusCodes.Status200OK)
      .AllowAnonymous()
      .WithSummary("Sign in")
      .WithDescription("Authenticates a user, returns an access token and sets the refresh cookie.");
    group.MapPost("/refresh", Refresh)
      .Produces<AuthResponse>(StatusCodes.Status200OK)
      .AllowAnonymous()
      .WithSummary("Refresh the session")
      .WithDescription("Exchanges the refresh cookie for a new access token and a new refresh cookie. A refresh token used twice revokes its whole sign-in. Requires the X-Requested-With header.");
    group.MapPost("/logout", Logout)
      .Produces(StatusCodes.Status204NoContent)
      .AllowAnonymous()
      .WithSummary("Sign out")
      .WithDescription("Revokes the sign-in of the refresh cookie and clears it. Requires the X-Requested-With header.");
    group.MapGet("/me", GetCurrentUser)
      .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
      .WithSummary("Get current user")
      .WithDescription("Returns the user attached to the bearer token.");

    return app;
  }

  private static async Task<IResult> Register(
    RegisterRequest request,
    HttpResponse response,
    IAuthService authService,
    CancellationToken cancellationToken)
  {
    var result = await authService.RegisterAsync(request, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.Error.ToHttpResult();
    }

    RefreshTokenCookie.Write(response, result.Value);
    return Results.Created("/auth/me", result.Value.Response);
  }

  private static async Task<IResult> Login(
    LoginRequest request,
    HttpResponse response,
    IAuthService authService,
    CancellationToken cancellationToken)
  {
    var result = await authService.LoginAsync(request, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.Error.ToHttpResult();
    }

    RefreshTokenCookie.Write(response, result.Value);
    return Results.Ok(result.Value.Response);
  }

  private static async Task<IResult> Refresh(
    HttpContext context,
    IAuthService authService,
    CancellationToken cancellationToken)
  {
    if (!RefreshTokenCookie.HasCsrfHeader(context.Request))
    {
      return MissingCsrfHeader();
    }

    var result = await authService.RefreshAsync(RefreshTokenCookie.Read(context.Request), cancellationToken);

    if (!result.IsSuccess)
    {
      RefreshTokenCookie.Clear(context.Response);
      return result.Error.ToHttpResult();
    }

    RefreshTokenCookie.Write(context.Response, result.Value);
    return Results.Ok(result.Value.Response);
  }

  private static async Task<IResult> Logout(
    HttpContext context,
    IAuthService authService,
    CancellationToken cancellationToken)
  {
    if (!RefreshTokenCookie.HasCsrfHeader(context.Request))
    {
      return MissingCsrfHeader();
    }

    await authService.LogoutAsync(RefreshTokenCookie.Read(context.Request), cancellationToken);
    RefreshTokenCookie.Clear(context.Response);

    return Results.NoContent();
  }

  private static IResult MissingCsrfHeader() =>
    Results.Problem(
      $"Send the {RefreshTokenCookie.CsrfHeader} header with requests that use the refresh cookie.",
      statusCode: StatusCodes.Status403Forbidden);

  private static IResult GetCurrentUser(ClaimsPrincipal user)
  {
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    var email = user.FindFirstValue(ClaimTypes.Email);
    var name = user.FindFirstValue(ClaimTypes.Name);

    if (string.IsNullOrWhiteSpace(userId)
      || string.IsNullOrWhiteSpace(email)
      || string.IsNullOrWhiteSpace(name))
    {
      return Results.Unauthorized();
    }

    return Results.Ok(new CurrentUserResponse(userId, email, name));
  }
}
