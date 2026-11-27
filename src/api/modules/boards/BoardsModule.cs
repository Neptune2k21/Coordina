using Coordina.Api.Core.Modules;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Infrastructure;
using Coordina.Api.Modules.Workspaces.Application;

namespace Coordina.Api.Modules.Boards;

public sealed class BoardsModule : IModule
{
  /// <summary>Whether the server records the forecasts of the day; on by default.</summary>
  public const string SnapshotSetting = "Forecasts:RecordDaily";

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
    services.AddScoped<IBoardMilestoneService, BoardMilestoneService>();
    services.AddScoped<IBoardMilestoneScopeService, BoardMilestoneScopeService>();
    services.AddScoped<IBoardMilestoneStaffingService, BoardMilestoneStaffingService>();
    services.AddScoped<IBoardHistoryStore, PostgresBoardHistoryStore>();
    services.AddScoped<BoardPlanReader>();
    services.AddScoped<ForecastSnapshotRecorder>();
    services.AddScoped<IWorkspaceDepartureHandler, BoardAssigneeRemoval>();
    services.AddSingleton<IGraphEngine<Guid>, NativeGuidGraphEngine>();

    // Tests record when they mean to, on databases that come and go.
    if (configuration.GetValue(SnapshotSetting, true))
    {
      services.AddHostedService<ForecastSnapshotWorker>();
    }
  }

  public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
    endpoints.MapBoardsEndpoints();
}
