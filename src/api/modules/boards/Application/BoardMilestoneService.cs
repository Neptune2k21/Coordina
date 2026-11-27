using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

internal sealed class BoardMilestoneService(
  BoardPlanReader plans,
  IGraphEngine<Guid> graphEngine) : IBoardMilestoneService
{
  public async Task<Result<IReadOnlyCollection<BoardMilestoneResponse>>> GetMilestonesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var plan = await plans.ReadAsync(
      new BoardTarget(workspaceId, projectId, boardId, userId),
      [],
      cancellationToken);

    return plan.IsSuccess
      ? Result.Success<IReadOnlyCollection<BoardMilestoneResponse>>(
        BoardMilestoneForecasts.ToResponses(
          plan.Value,
          BoardMilestoneForecasts.Forecast(graphEngine, plan.Value)))
      : plan.Error;
  }
}
