using Coordina.Api.Core.Modules;
using Coordina.Api.Modules.Insights.Application;

namespace Coordina.Api.Modules.Insights;

/// <summary>What the graph engine makes of a board, for people and assistants; it only reads.</summary>
public sealed class InsightsModule : IModule
{
  public void AddServices(
    IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddScoped<IInsightsService, InsightsService>();
  }

  public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
    endpoints.MapInsightsEndpoints();
}
