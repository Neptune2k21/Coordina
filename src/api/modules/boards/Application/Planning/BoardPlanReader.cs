using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application.Planning;

/// <summary>Reads a board as the engine plans it.</summary>
internal sealed class BoardPlanReader(
  IBoardStore boards,
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

  // The board is all a plan reads, so this is synchronous for now.
  public Task<BoardPlan> PlanAsync(
    ProjectBoard board,
    IReadOnlyCollection<Guid> newcomers,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return Task.FromResult(BoardPlans.Build(board, newcomers, clock.GetUtcNow()));
  }
}
