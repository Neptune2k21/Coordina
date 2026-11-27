using Coordina.Api.Core.Http;
using Coordina.Api.Core.Security;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Insights.Application;
using Coordina.Api.Modules.Insights.Contracts;

namespace Coordina.Api.Modules.Insights;

/// <summary>
/// What the engine makes of a board. Every endpoint only reads, returns
/// structured values rather than prose, and measures on the same simulation,
/// so an assistant can chain them: read the plan, ask, try changes, then make
/// them through the endpoints that write.
/// </summary>
public static class InsightsEndpoints
{
  private const int DefaultEventLimit = 100;

  public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder app)
  {
    var board = app.MapApiGroup(
      "/workspaces/{workspaceId:guid}/projects/{projectId:guid}/boards/{boardId:guid}",
      "Board insights");

    board.MapGet("/plan", GetPlan)
      .Produces<BoardPlanResponse>(StatusCodes.Status200OK)
      .WithSummary("Get the board's plan")
      .WithDescription("Returns every open milestone's forecast, the cards of their scopes in the order to pick them up with who is expected to take each and when, and what the forecast assumes. The milestones share one team.");

    board.MapPost("/what-if", TryChanges)
      .Produces<BoardWhatIfResponse>(StatusCodes.Status200OK)
      .WithSummary("Try changes on a board")
      .WithDescription("Returns the milestones' forecasts as they are and as they would be with the changes, on the same simulated runs, so the difference comes from the changes alone. Saves nothing. Changes that make cards depend on each other in a loop are refused (409).");

    board.MapGet("/learning", GetLearning)
      .Produces<BoardLearningResponse>(StatusCodes.Status200OK)
      .WithSummary("Get what the board has learned")
      .WithDescription("Returns how long this team's cards take against what their size says, by size and label, and how often the board's past forecasts were met. Nothing in it compares people.");

    board.MapGet("/events", ListEvents)
      .Produces<BoardEventPageResponse>(StatusCodes.Status200OK)
      .WithSummary("List what happened to the board's cards")
      .WithDescription("Returns a page of the board's history, oldest first: cards created, moved between task statuses, sized, linked, assigned or deleted. Pass nextAfter as after to get the next page.");

    var milestone = board.MapGroup("/milestones/{milestoneId:guid}");

    milestone.MapGet("/actions", ProposeActions)
      .Produces<IReadOnlyCollection<BoardMilestoneActionResponse>>(StatusCodes.Status200OK)
      .WithSummary("Rank what to do about a milestone")
      .WithDescription("Returns the single steps that help the milestone most, the best first: taking a card out, giving a card to someone of the team, adding a person. Each is measured by forecasting the board with it, so they compare. Empty when nothing helps.");

    milestone.MapPost("/questions", RankQuestions)
      .Produces<BoardMilestoneQuestionsResponse>(StatusCodes.Status200OK)
      .WithSummary("Rank what to ask about a milestone")
      .WithDescription("Returns the few answers that would make the milestone's date surest: a card to size or to split, a card in progress that drags on, a dependency that may be missing. dependencyHints adds dependencies someone suspects; they are weighed, not saved.");

    return app;
  }

  private static async Task<IResult> GetPlan(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CurrentUser user,
    IInsightsService insights,
    CancellationToken cancellationToken)
  {
    var result = await insights.GetPlanAsync(
      new BoardTarget(workspaceId, projectId, boardId, user.Id),
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> TryChanges(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    BoardWhatIfRequest request,
    CurrentUser user,
    IInsightsService insights,
    CancellationToken cancellationToken)
  {
    var result = await insights.TryChangesAsync(
      new BoardTarget(workspaceId, projectId, boardId, user.Id),
      request,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> GetLearning(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CurrentUser user,
    IInsightsService insights,
    CancellationToken cancellationToken)
  {
    var result = await insights.GetLearningAsync(
      new BoardTarget(workspaceId, projectId, boardId, user.Id),
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> ListEvents(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    long? after,
    int? limit,
    CurrentUser user,
    IInsightsService insights,
    CancellationToken cancellationToken)
  {
    var result = await insights.ListEventsAsync(
      new BoardTarget(workspaceId, projectId, boardId, user.Id),
      after ?? 0,
      limit ?? DefaultEventLimit,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> ProposeActions(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    CurrentUser user,
    IInsightsService insights,
    CancellationToken cancellationToken)
  {
    var result = await insights.ProposeActionsAsync(
      new BoardTarget(workspaceId, projectId, boardId, user.Id),
      milestoneId,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> RankQuestions(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneQuestionsRequest request,
    CurrentUser user,
    IInsightsService insights,
    CancellationToken cancellationToken)
  {
    var result = await insights.RankQuestionsAsync(
      new BoardTarget(workspaceId, projectId, boardId, user.Id),
      milestoneId,
      request,
      cancellationToken);

    return result.ToOkResult();
  }
}
