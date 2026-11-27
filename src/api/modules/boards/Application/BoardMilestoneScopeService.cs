using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

internal sealed class BoardMilestoneScopeService(
  IBoardStore boards,
  BoardPlanReader plans,
  IProjectAccessGuard projectAccess,
  IGraphEngine<Guid> graphEngine,
  TimeProvider clock) : IBoardMilestoneScopeService
{
  public Task<Result<BoardMilestoneScopeResponse>> ProposeAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneScopeRequest request,
    Guid userId,
    CancellationToken cancellationToken) =>
    MeasureAsync(
      new BoardTarget(workspaceId, projectId, boardId, userId),
      plan => BoardMilestoneScopes.Propose(graphEngine, plan, milestoneId, request.Keep ?? []),
      cancellationToken);

  public Task<Result<BoardMilestoneScopeResponse>> EvaluateAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneCutRequest request,
    Guid userId,
    CancellationToken cancellationToken) =>
    MeasureAsync(
      new BoardTarget(workspaceId, projectId, boardId, userId),
      plan => BoardMilestoneScopes.Evaluate(graphEngine, plan, milestoneId, request.CardIds ?? []),
      cancellationToken);

  public async Task<Result<BoardResponse>> CutAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneCutRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.AuthorizeWriteAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    if (request.CardIds is not { Count: > 0 } cardIds)
    {
      return Error.Validation(nameof(request.CardIds), "Choose at least one card to take out.");
    }

    return await boards.RunExclusiveAsync<Result<BoardResponse>>(
      boardId,
      async exclusiveToken =>
      {
        var board = await boards.FindInProjectAsync(workspaceId, projectId, boardId, exclusiveToken);

        if (board is null)
        {
          return Error.NotFound();
        }

        var cut = BoardMilestoneScopes.Evaluate(
          graphEngine,
          await plans.PlanAsync(board, [], exclusiveToken),
          milestoneId,
          cardIds);

        if (!cut.IsSuccess)
        {
          return cut.Error;
        }

        await DropDependenciesAsync(new BoardTarget(workspaceId, projectId, boardId, userId), cut.Value, exclusiveToken);
        var updated = await boards.FindInProjectAsync(workspaceId, projectId, boardId, exclusiveToken);

        return updated is null ? Error.NotFound() : Result.Success(BoardResponseMapper.ToResponse(updated));
      },
      cancellationToken);
  }

  // Every path from the milestone to a card that leaves goes through a card
  // that leaves too, so dropping its direct dependencies on them takes the
  // whole cut out.
  private async Task DropDependenciesAsync(
    BoardTarget target,
    ScopeCut cut,
    CancellationToken cancellationToken)
  {
    var dropped = cut.DroppedCardIds.ToHashSet();
    var dependencies = cut.Milestone.Dependencies
      .Select(dependency => dependency.CardId)
      .Where(dropped.Contains)
      .ToArray();

    if (dependencies.Length > 0)
    {
      await boards.DeleteCardDependenciesAsync(
        target.WorkspaceId,
        target.ProjectId,
        target.BoardId,
        cut.Milestone.Id,
        dependencies,
        clock.GetUtcNow(),
        cancellationToken);
    }
  }

  // Measuring a cut changes nothing, so reading the board is enough.
  private async Task<Result<BoardMilestoneScopeResponse>> MeasureAsync(
    BoardTarget target,
    Func<BoardPlan, Result<ScopeCut>> measure,
    CancellationToken cancellationToken)
  {
    var plan = await plans.ReadAsync(target, [], cancellationToken);

    if (!plan.IsSuccess)
    {
      return plan.Error;
    }

    var cut = measure(plan.Value);

    return cut.IsSuccess ? Result.Success(cut.Value.ToResponse()) : cut.Error;
  }
}
