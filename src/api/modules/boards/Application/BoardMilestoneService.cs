using Coordina.Api.Core.Results;
using Coordina.Api.Core.Time;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

public sealed class BoardMilestoneService(
  IBoardStore boards,
  IProjectAccessGuard projectAccess,
  IGraphEngine<Guid> graphEngine,
  TimeProvider clock) : IBoardMilestoneService
{
  public async Task<Result<IReadOnlyCollection<BoardMilestoneResponse>>> GetMilestonesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.AuthorizeReadAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    var board = await boards.FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success<IReadOnlyCollection<BoardMilestoneResponse>>(
        BoardMilestoneForecasts.Forecast(graphEngine, board, clock.GetUtcToday()));
  }
}
