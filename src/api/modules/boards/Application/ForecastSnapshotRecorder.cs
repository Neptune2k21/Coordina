using Coordina.Api.Core.Time;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>Keeps what the forecasts say each day, so they can be checked once their milestones are done.</summary>
internal sealed partial class ForecastSnapshotRecorder(
  IBoardStore boards,
  IBoardHistoryStore history,
  BoardPlanReader plans,
  IGraphEngine<Guid> graphEngine,
  TimeProvider clock,
  ILogger<ForecastSnapshotRecorder> logger)
{
  private const int PageSize = 50;

  /// <summary>
  /// Records today's forecasts of the next boards that have none yet. Returns
  /// the last board it looked at, to go on from, or null when there are no more.
  /// </summary>
  public async Task<Guid?> RecordAsync(Guid afterBoardId, CancellationToken cancellationToken)
  {
    var page = await history.ListBoardsWithoutSnapshotAsync(
      clock.GetUtcToday(),
      afterBoardId,
      PageSize,
      cancellationToken);

    foreach (var key in page)
    {
      await RecordBoardAsync(key, cancellationToken);
    }

    return page.Count == PageSize ? page[^1].BoardId : null;
  }

  // One board that cannot be forecast must not keep the others from it.
  private async Task RecordBoardAsync(BoardKey key, CancellationToken cancellationToken)
  {
    try
    {
      var board = await boards.FindInProjectAsync(
        key.WorkspaceId,
        key.ProjectId,
        key.BoardId,
        cancellationToken);

      // Deleted since it was listed.
      if (board is null)
      {
        return;
      }

      var plan = await plans.PlanAsync(board, [], cancellationToken);
      var forecast = BoardMilestoneForecasts.Forecast(graphEngine, plan);
      await history.SaveSnapshotsAsync(
        board.Id,
        BoardMilestoneForecasts.ToSnapshots(plan, forecast),
        cancellationToken);
    }
    catch (GraphEngineException exception)
    {
      LogFailure(logger, key.BoardId, exception);
    }
  }

  [LoggerMessage(LogLevel.Error, "The forecasts of board {BoardId} could not be recorded.")]
  private static partial void LogFailure(ILogger logger, Guid boardId, Exception exception);
}
