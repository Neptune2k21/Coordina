using Coordina.Api.Core.Modules;
using Coordina.Api.Modules.Health.Infrastructure;

namespace Coordina.Api.Modules.Health;

public sealed class HealthModule : IModule
{
  public void AddServices(IServiceCollection services, IConfiguration configuration) =>
    services
      .AddHealthChecks()
      .AddDatabaseCheck(HealthEndpoints.ReadinessTag);

  public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
    endpoints.MapHealthEndpoints();
}
