using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Insights.Contracts;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Insights.Application;

internal sealed class InsightsService(
  BoardPlanReader plans,
  IBoardStore boards,
  IBoardHistoryStore history,
  IProjectAccessGuard projectAccess,
  IGraphEngine<Guid> graphEngine) : IInsightsService
{
  public const int EventLimit = 500;

  public async Task<Result<BoardLearningResponse>> GetLearningAsync(
    BoardTarget target,
    CancellationToken cancellationToken)
  {
    var plan = await plans.ReadAsync(target, [], cancellationToken);

    if (!plan.IsSuccess)
    {
      return plan.Error;
    }

    var snapshots = await history.ListSnapshotsAsync(target.BoardId, cancellationToken);

    return Result.Success(BoardLearning.ToResponse(
      plan.Value,
      graphEngine.Calibrate(plan.Value.Graph),
      snapshots));
  }

  public async Task<Result<BoardEventPageResponse>> ListEventsAsync(
    BoardTarget target,
    long after,
    int limit,
    CancellationToken cancellationToken)
  {
    if (limit is < 1 or > EventLimit)
    {
      return Error.Validation(nameof(limit), $"The limit must be between 1 and {EventLimit}.");
    }

    var board = await boards.FindReadableAsync(projectAccess, target, cancellationToken);

    if (!board.IsSuccess)
    {
      return board.Error;
    }

    var events = await history.ListEventsAsync(target.BoardId, after, limit, cancellationToken);

    return Result.Success(new BoardEventPageResponse(
      events.Select(BoardInsights.ToResponse).ToArray(),
      events.Count == limit ? events[^1].Id : null));
  }
}
