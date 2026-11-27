using Coordina.Api.Core.Http;
using Coordina.Api.Core.Security;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Insights.Application;
using Coordina.Api.Modules.Insights.Contracts;

namespace Coordina.Api.Modules.Insights;

/// <summary>
/// What the engine makes of a board. Every endpoint only reads and returns
/// structured values rather than prose.
/// </summary>
public static class InsightsEndpoints
{
  private const int DefaultEventLimit = 100;

  public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder app)
  {
    var board = app.MapApiGroup(
      "/workspaces/{workspaceId:guid}/projects/{projectId:guid}/boards/{boardId:guid}",
      "Board insights");

    board.MapGet("/learning", GetLearning)
      .Produces<BoardLearningResponse>(StatusCodes.Status200OK)
      .WithSummary("Get what the board has learned")
      .WithDescription("Returns how long this team's cards take against what their size says, by size and label, and how often the board's past forecasts were met. Nothing in it compares people.");

    board.MapGet("/events", ListEvents)
      .Produces<BoardEventPageResponse>(StatusCodes.Status200OK)
      .WithSummary("List what happened to the board's cards")
      .WithDescription("Returns a page of the board's history, oldest first: cards created, moved between task statuses, sized, linked, assigned or deleted. Pass nextAfter as after to get the next page.");

    return app;
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
}
