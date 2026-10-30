using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Coordina.Api.Modules.Health;

public static class HealthEndpoints
{
  /// <summary>Tag of the checks that must pass before the API takes traffic.</summary>
  public const string ReadinessTag = "ready";

  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
  {
    app.MapGet("/", () => Results.Ok(new
    {
      message = "Welcome to the Coordina API."
    }))
    .AllowAnonymous()
    .WithTags("System")
    .WithSummary("API welcome message");

    // Liveness runs no check: it only proves the process answers requests, so
    // an orchestrator does not restart the API while the database is down.
    app.MapHealthCheck("/health/live", _ => false);

    // Readiness answers 503 while a dependency (PostgreSQL) is unreachable.
    app.MapHealthCheck("/health/ready", check => check.Tags.Contains(ReadinessTag));

    return app;
  }

  private static void MapHealthCheck(
    this IEndpointRouteBuilder app,
    string pattern,
    Func<HealthCheckRegistration, bool> predicate) =>
    app.MapHealthChecks(pattern, new HealthCheckOptions { Predicate = predicate })
      .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]))
      .AllowAnonymous();
}
