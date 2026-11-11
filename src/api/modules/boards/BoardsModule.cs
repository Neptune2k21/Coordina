using Coordina.Api.Core.Modules;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Infrastructure;
using Coordina.Api.Modules.Workspaces.Application;

namespace Coordina.Api.Modules.Boards;

public sealed class BoardsModule : IModule
{
  public void AddServices(
    IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddScoped<IBoardStore, PostgresBoardStore>();
    services.AddScoped<IBoardCatalogService, BoardCatalogService>();
    services.AddScoped<IBoardListService, BoardListService>();
    services.AddScoped<IBoardCardService, BoardCardService>();
    services.AddScoped<IBoardCollaborationService, BoardCollaborationService>();
    services.AddScoped<IBoardDependencyService, BoardDependencyService>();
    services.AddScoped<IBoardPlanningPreferenceStore, PostgresBoardPlanningPreferenceStore>();
    services.AddScoped<IBoardPlanningService, BoardPlanningService>();
    services.AddScoped<IWorkspaceDepartureHandler, BoardAssigneeRemoval>();
    services.AddSingleton<IGraphEngine<Guid>, NativeGuidGraphEngine>();
  }

  public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
    endpoints.MapBoardsEndpoints();
}
