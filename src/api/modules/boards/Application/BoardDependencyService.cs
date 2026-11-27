using Coordina.Api.Core.Results;
using Coordina.Api.Core.Time;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

internal sealed class BoardDependencyService(
  IBoardStore boards,
  BoardPlanReader plans,
  IProjectAccessGuard projectAccess,
  IGraphEngine<Guid> graphEngine,
  TimeProvider clock) : IBoardDependencyService
{
  private const int SuggestionLimit = 8;

  public async Task<Result<BoardGraphResponse>> GetGraphAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    BoardPlanningRequest planning,
    CancellationToken cancellationToken)
  {
    var preferences = BoardPlanningPreferences.Parse(planning, userId);

    if (!preferences.IsSuccess)
    {
      return preferences.Error;
    }

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
      : Result.Success(BoardGraphResponseMapper.ToResponse(
        BoardDependencyGraph.AnalyzeBoard(
          graphEngine,
          board,
          preferences.Value,
          clock.GetUtcToday())));
  }

  public async Task<Result<BoardCardDependencyAnalysisResponse>> GetCardDependencyAnalysisAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var plan = await plans.ReadAsync(
      new BoardTarget(workspaceId, projectId, boardId, userId),
      [],
      cancellationToken);

    if (!plan.IsSuccess)
    {
      return plan.Error;
    }

    var suggestions = graphEngine.SuggestDependencies(
      plan.Value.Graph,
      CardTokens.Of(plan.Value.Cards.Values),
      cardId,
      SuggestionLimit);
    var analysis = BoardDependencyGraph.AnalyzeCard(
      graphEngine,
      plan.Value.Board,
      cardId,
      suggestions.Select(hint => hint.Dependency).ToArray());

    return analysis is null
      ? Error.NotFound()
      : Result.Success(BoardGraphResponseMapper.ToResponse(analysis));
  }

  public async Task<Result<BoardResponse>> AddCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    AddBoardCardDependencyRequest request,
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

    if (request.DependsOnCardId == Guid.Empty || request.DependsOnCardId == cardId)
    {
      return Error.Validation(
        nameof(request.DependsOnCardId),
        "Choose another card from this board.");
    }

    return await boards.RunExclusiveAsync<Result<BoardResponse>>(
      boardId,
      async exclusiveToken =>
      {
        var boardSnapshot = await boards.FindInProjectAsync(
          workspaceId,
          projectId,
          boardId,
          exclusiveToken);

        if (boardSnapshot is null)
        {
          return Error.NotFound();
        }

        var dependencyEvaluation = BoardDependencyGraph.EvaluateDependency(
          graphEngine,
          boardSnapshot,
          cardId,
          request.DependsOnCardId);

        switch (dependencyEvaluation.Status)
        {
          case GraphEdgeStatus.MissingSource or GraphEdgeStatus.MissingTarget:
            return Error.NotFound();
          case GraphEdgeStatus.Duplicate:
            return Result.Success(BoardResponseMapper.ToResponse(boardSnapshot));
          case GraphEdgeStatus.Cycle:
            return Error.Conflict("Adding this dependency would create a cycle.");
        }

        var board = await boards.AddCardDependencyAsync(
          workspaceId,
          projectId,
          boardId,
          cardId,
          request.DependsOnCardId,
          clock.GetUtcNow(),
          exclusiveToken);

        return board is null
          ? Error.NotFound()
          : Result.Success(BoardResponseMapper.ToResponse(board));
      },
      cancellationToken);
  }

  public async Task<Result> DeleteCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
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

    var deleted = await boards.DeleteCardDependenciesAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      [dependsOnCardId],
      clock.GetUtcNow(),
      cancellationToken);

    return deleted > 0 ? Result.Success() : Error.NotFound();
  }
}
