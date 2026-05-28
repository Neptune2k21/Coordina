namespace Coordina.Api.Modules.Health;

public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
  {
    app.MapGet("/health", () => Results.Ok(new
    {
      status = "tout est ok",
      service = "Coordina API",
      time = DateTime.UtcNow
    }));

    return app;
  }
}
