using Coordina.Api.Core.Modules;
using Coordina.Api.Modules.Work.Application;

namespace Coordina.Api.Modules.Work;

/// <summary>"My work": the user's next tasks across a workspace, built on the boards' planning.</summary>
public sealed class WorkModule : IModule
{
  public void AddServices(IServiceCollection services, IConfiguration configuration) =>
    services.AddScoped<IMyWorkService, MyWorkService>();

  public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
    endpoints.MapWorkEndpoints();
}
