using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application.Planning;

/// <summary>Reads a board with the history its plan needs.</summary>
internal sealed class BoardPlanReader(
  IBoardStore boards,
  IBoardHistoryStore history,
  IProjectAccessGuard projectAccess,
  TimeProvider clock)
{
  /// <summary>The plan of a board the user may read; not found otherwise.</summary>
  public async Task<Result<BoardPlan>> ReadAsync(
    BoardTarget target,
    IReadOnlyCollection<Guid> newcomers,
    CancellationToken cancellationToken)
  {
    var board = await boards.FindReadableAsync(projectAccess, target, cancellationToken);

    return board.IsSuccess
      ? Result.Success(await PlanAsync(board.Value, newcomers, cancellationToken))
      : board.Error;
  }

  public async Task<BoardPlan> PlanAsync(
    ProjectBoard board,
    IReadOnlyCollection<Guid> newcomers,
    CancellationToken cancellationToken)
  {
    var events = await history.ListStatusEventsAsync(board.Id, cancellationToken);

    return BoardPlans.Build(board, events, newcomers, StartOfHour(clock.GetUtcNow()));
  }

  // Cards in progress are counted to the hour, so a forecast does not drift
  // from one load to the next.
  private static DateTimeOffset StartOfHour(DateTimeOffset now) =>
    new(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerHour), TimeSpan.Zero);
}
