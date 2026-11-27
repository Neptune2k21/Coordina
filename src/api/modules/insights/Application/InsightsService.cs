using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Insights.Contracts;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Workspaces.Application;

namespace Coordina.Api.Modules.Insights.Application;

internal sealed class InsightsService(
  BoardPlanReader plans,
  IBoardStore boards,
  IBoardHistoryStore history,
  IProjectAccessGuard projectAccess,
  IWorkspaceStore workspaces,
  IGraphEngine<Guid> graphEngine) : IInsightsService
{
  public const int EventLimit = 500;

  private const int ActionLimit = 6;
  private const int ActionScreenRunCount = 128;
  private const int QuestionLimit = 5;
  private const int HintLimit = 200;
  private const string HintField = nameof(BoardMilestoneQuestionsRequest.DependencyHints);

  public async Task<Result<BoardPlanResponse>> GetPlanAsync(
    BoardTarget target,
    CancellationToken cancellationToken)
  {
    var plan = await plans.ReadAsync(target, [], cancellationToken);

    return plan.IsSuccess
      ? Result.Success(BoardInsights.ToResponse(
        plan.Value,
        BoardMilestoneForecasts.Forecast(graphEngine, plan.Value),
        graphEngine.Calibrate(plan.Value.Graph)))
      : plan.Error;
  }

  public async Task<Result<IReadOnlyCollection<BoardMilestoneActionResponse>>> ProposeActionsAsync(
    BoardTarget target,
    Guid milestoneId,
    CancellationToken cancellationToken)
  {
    var plan = await ReadOpenMilestoneAsync(target, milestoneId, cancellationToken);

    if (!plan.IsSuccess)
    {
      return plan.Error;
    }

    var actions = graphEngine.ProposeActions(
      plan.Value.Graph,
      ReceiversOf(plan.Value),
      new GraphActionOptions<Guid>(milestoneId, BoardPlan.Simulation, ActionScreenRunCount, ActionLimit));

    return Result.Success<IReadOnlyCollection<BoardMilestoneActionResponse>>(
      actions.Select(action => BoardInsights.ToResponse(plan.Value, action)).ToArray());
  }

  public async Task<Result<BoardMilestoneQuestionsResponse>> RankQuestionsAsync(
    BoardTarget target,
    Guid milestoneId,
    BoardMilestoneQuestionsRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var plan = await ReadOpenMilestoneAsync(target, milestoneId, cancellationToken);

    if (!plan.IsSuccess)
    {
      return plan.Error;
    }

    var hints = request.DependencyHints ?? [];

    if (CheckHints(hints, plan.Value) is { } problem)
    {
      return problem;
    }

    var questions = graphEngine.RankQuestions(
      plan.Value.Graph,
      CardTokens.Of(plan.Value.Cards.Values),
      hints
        .Select(hint => new GraphDependencyHint<Guid>(hint.CardId, hint.DependsOnCardId, hint.Probability))
        .ToArray(),
      new GraphQuestionOptions<Guid>(milestoneId, BoardPlan.Simulation, QuestionLimit));

    return Result.Success(new BoardMilestoneQuestionsResponse(
      milestoneId,
      questions.MarginDays,
      questions.Questions.Select(question => BoardInsights.ToResponse(plan.Value, question)).ToArray()));
  }

  public async Task<Result<BoardWhatIfResponse>> TryChangesAsync(
    BoardTarget target,
    BoardWhatIfRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var members = (await workspaces.ListMembersAsync(target.WorkspaceId, cancellationToken))
      .Select(member => member.UserId)
      .ToHashSet();
    var plan = await plans.ReadAsync(target, members, cancellationToken);

    if (!plan.IsSuccess)
    {
      return plan.Error;
    }

    var scenario = BoardScenario.Apply(plan.Value.Board, request.Changes ?? [], members, plan.Value.Today);

    if (!scenario.IsSuccess)
    {
      return scenario.Error;
    }

    var changed = await plans.PlanAsync(scenario.Value.Board, members, cancellationToken);

    if (!graphEngine.IsAcyclic(changed.Graph.Nodes, changed.Graph.Edges))
    {
      return Error.Conflict("These changes would make cards depend on each other in a loop.");
    }

    var after = changed with { Graph = changed.Graph with { ExtraPeople = scenario.Value.ExtraPeople } };

    return Result.Success(new BoardWhatIfResponse(Milestones(plan.Value), Milestones(after)));
  }

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

  private BoardMilestoneResponse[] Milestones(BoardPlan plan) =>
    BoardMilestoneForecasts.ToResponses(plan, BoardMilestoneForecasts.Forecast(graphEngine, plan));

  private async Task<Result<BoardPlan>> ReadOpenMilestoneAsync(
    BoardTarget target,
    Guid milestoneId,
    CancellationToken cancellationToken)
  {
    var plan = await plans.ReadAsync(target, [], cancellationToken);

    if (!plan.IsSuccess)
    {
      return plan.Error;
    }

    return plan.Value.FindMilestone(milestoneId) switch
    {
      null => Error.NotFound(),
      { IsCompleted: true } => Error.Validation("milestoneId", "This milestone is done."),
      _ => plan
    };
  }

  // The people already assigned to open cards of the board.
  private static int[] ReceiversOf(BoardPlan plan) =>
    BoardMilestoneStaffings.TeamOf(plan.Board).Select(plan.PersonOf).ToArray();

  private static Error? CheckHints(
    IReadOnlyCollection<BoardDependencyHintRequest> hints,
    BoardPlan plan)
  {
    if (hints.Count > HintLimit)
    {
      return Error.Validation(HintField, $"Give at most {HintLimit} hints.");
    }

    if (hints.Any(hint => !plan.Cards.ContainsKey(hint.CardId) || !plan.Cards.ContainsKey(hint.DependsOnCardId)))
    {
      return Error.Validation(HintField, "Every hint must name two cards of this board.");
    }

    return hints.Any(hint => hint.Probability is not (>= 0 and <= 1))
      ? Error.Validation(HintField, "A probability goes from 0 to 1.")
      : null;
  }
}
