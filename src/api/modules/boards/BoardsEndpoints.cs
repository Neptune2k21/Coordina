using Coordina.Api.Core.Http;
using Coordina.Api.Core.Security;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards;

public static class BoardsEndpoints
{
  public static IEndpointRouteBuilder MapBoardsEndpoints(
    this IEndpointRouteBuilder app)
  {
    app.MapApiGroup("/board-templates", "Boards")
      .MapGet("/", ListTemplates)
      .Produces<IReadOnlyCollection<BoardTemplateResponse>>(StatusCodes.Status200OK)
      .WithSummary("List board templates")
      .WithDescription("Returns every template with its lists and example cards.");

    var projectGroup = app.MapApiGroup(
      "/workspaces/{workspaceId:guid}/projects/{projectId:guid}",
      "Boards");

    projectGroup.MapGet("/board", GetDefault)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Get default project board")
      .WithDescription("Returns a full board with lists, cards, labels, and assignees.");

    projectGroup.MapPost("/boards", CreateBoard)
      .Produces<BoardResponse>(StatusCodes.Status201Created)
      .WithSummary("Create board")
      .WithDescription("Creates a project board and generates lists from the selected template.");

    var boardGroup = projectGroup.MapGroup("/boards/{boardId:guid}");

    boardGroup.MapGet("/graph", GetGraph)
      .Produces<BoardGraphResponse>(StatusCodes.Status200OK)
      .WithSummary("Get board dependency graph")
      .WithDescription("Returns graph-derived planning signals for the board.");

    boardGroup.MapGet("/planning-preferences", GetPlanningPreferences)
      .Produces<BoardPlanningPreferencesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get my planning preferences")
      .WithDescription("Returns the signed-in user's planning preferences for the board, or the defaults when none are saved.");

    boardGroup.MapPut("/planning-preferences", SavePlanningPreferences)
      .Produces<BoardPlanningPreferencesResponse>(StatusCodes.Status200OK)
      .WithSummary("Save my planning preferences")
      .WithDescription("Replaces the signed-in user's planning preferences for the board. Fields left out take their default.");

    boardGroup.MapPost("/lists", CreateList)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Create board list");

    boardGroup.MapPatch("/lists/{listId:guid}", UpdateList)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Update board list")
      .WithDescription("Renames a list, changes its task status or moves it. A status change completes or reopens its cards, as moving them would.");

    boardGroup.MapDelete("/lists/{listId:guid}", DeleteList)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Delete board list")
      .WithDescription("Deletes a list. Its cards move to the list given by moveCardsTo, which is required when it has cards.");

    boardGroup.MapPost("/lists/{listId:guid}/cards", CreateCard)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Create card");

    boardGroup.MapPatch("/cards/{cardId:guid}", UpdateCard)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Update card");

    boardGroup.MapPatch("/cards/{cardId:guid}/move", MoveCard)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Move card");

    boardGroup.MapGet("/cards/{cardId:guid}/dependency-analysis", GetCardDependencyAnalysis)
      .Produces<BoardCardDependencyAnalysisResponse>(StatusCodes.Status200OK)
      .WithSummary("Get card dependency analysis")
      .WithDescription("Returns safe dependency suggestions, blockers, and impacted dependents.");

    boardGroup.MapPost("/cards/{cardId:guid}/comments", AddCardComment)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Add card comment");

    boardGroup.MapPatch("/cards/{cardId:guid}/comments/{commentId:guid}", UpdateCardComment)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Edit card comment")
      .WithDescription("Replaces the comment's text and records editedAt. Only the author can edit (403 for others).");

    boardGroup.MapDelete("/cards/{cardId:guid}/comments/{commentId:guid}", DeleteCardComment)
      .Produces(StatusCodes.Status204NoContent)
      .WithSummary("Delete card comment")
      .WithDescription("Deletes the comment. Only the author or a workspace OWNER can delete (403 for others).");

    boardGroup.MapPost("/cards/{cardId:guid}/subtasks", CreateCardSubtask)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Create card subtask");

    boardGroup.MapPatch("/cards/{cardId:guid}/subtasks/{subtaskId:guid}", UpdateCardSubtask)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Update card subtask");

    boardGroup.MapDelete("/cards/{cardId:guid}/subtasks/{subtaskId:guid}", DeleteCardSubtask)
      .Produces(StatusCodes.Status204NoContent)
      .WithSummary("Delete card subtask");

    boardGroup.MapPost("/cards/{cardId:guid}/dependencies", AddCardDependency)
      .Produces<BoardResponse>(StatusCodes.Status200OK)
      .WithSummary("Add card dependency");

    boardGroup.MapDelete("/cards/{cardId:guid}/dependencies/{dependsOnCardId:guid}", DeleteCardDependency)
      .Produces(StatusCodes.Status204NoContent)
      .WithSummary("Delete card dependency");

    boardGroup.MapDelete("/cards/{cardId:guid}", DeleteCard)
      .Produces(StatusCodes.Status204NoContent)
      .WithSummary("Delete card");

    return app;
  }

  private static IResult ListTemplates(IBoardCatalogService boardCatalog) =>
    Results.Ok(boardCatalog.ListTemplates());

  private static async Task<IResult> GetDefault(
    Guid workspaceId,
    Guid projectId,
    CurrentUser user,
    IBoardCatalogService boardCatalog,
    CancellationToken cancellationToken)
  {
    var result = await boardCatalog.GetDefaultAsync(
      workspaceId,
      projectId,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> CreateBoard(
    Guid workspaceId,
    Guid projectId,
    CreateBoardRequest request,
    CurrentUser user,
    IBoardCatalogService boardCatalog,
    CancellationToken cancellationToken)
  {
    var result = await boardCatalog.CreateAsync(
      workspaceId,
      projectId,
      request,
      user.Id,
      cancellationToken);

    return result.ToCreatedResult(
      board => $"/workspaces/{workspaceId}/projects/{projectId}/boards/{board.Id}");
  }

  private static async Task<IResult> GetGraph(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    string? maxSize,
    string? context,
    bool? preferAssigned,
    bool? includeUnassigned,
    CurrentUser user,
    IBoardDependencyService boardDependencies,
    CancellationToken cancellationToken)
  {
    var result = await boardDependencies.GetGraphAsync(
      workspaceId,
      projectId,
      boardId,
      user.Id,
      new BoardPlanningRequest(
        maxSize,
        context,
        preferAssigned,
        includeUnassigned),
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> GetPlanningPreferences(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CurrentUser user,
    IBoardPlanningService planning,
    CancellationToken cancellationToken)
  {
    var result = await planning.GetPreferencesAsync(
      workspaceId,
      projectId,
      boardId,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> SavePlanningPreferences(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    BoardPlanningRequest request,
    CurrentUser user,
    IBoardPlanningService planning,
    CancellationToken cancellationToken)
  {
    var result = await planning.SavePreferencesAsync(
      workspaceId,
      projectId,
      boardId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> CreateList(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CreateBoardListRequest request,
    CurrentUser user,
    IBoardListService boardLists,
    CancellationToken cancellationToken)
  {
    var result = await boardLists.CreateListAsync(
      workspaceId,
      projectId,
      boardId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> UpdateList(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    UpdateBoardListRequest request,
    CurrentUser user,
    IBoardListService boardLists,
    CancellationToken cancellationToken)
  {
    var result = await boardLists.UpdateListAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> DeleteList(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    Guid? moveCardsTo,
    CurrentUser user,
    IBoardListService boardLists,
    CancellationToken cancellationToken)
  {
    var result = await boardLists.DeleteListAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      moveCardsTo,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> CreateCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    CreateBoardCardRequest request,
    CurrentUser user,
    IBoardCardService boardCards,
    CancellationToken cancellationToken)
  {
    var result = await boardCards.CreateCardAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> UpdateCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    UpdateBoardCardRequest request,
    CurrentUser user,
    IBoardCardService boardCards,
    CancellationToken cancellationToken)
  {
    var result = await boardCards.UpdateCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> MoveCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    MoveBoardCardRequest request,
    CurrentUser user,
    IBoardCardService boardCards,
    CancellationToken cancellationToken)
  {
    var result = await boardCards.MoveCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> GetCardDependencyAnalysis(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CurrentUser user,
    IBoardDependencyService boardDependencies,
    CancellationToken cancellationToken)
  {
    var result = await boardDependencies.GetCardDependencyAnalysisAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> AddCardComment(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardCommentRequest request,
    CurrentUser user,
    IBoardCollaborationService boardCollaboration,
    CancellationToken cancellationToken)
  {
    var result = await boardCollaboration.AddCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> UpdateCardComment(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    UpdateBoardCardCommentRequest request,
    CurrentUser user,
    IBoardCollaborationService boardCollaboration,
    CancellationToken cancellationToken)
  {
    var result = await boardCollaboration.UpdateCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      commentId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> DeleteCardComment(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    CurrentUser user,
    IBoardCollaborationService boardCollaboration,
    CancellationToken cancellationToken)
  {
    var result = await boardCollaboration.DeleteCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      commentId,
      user.Id,
      cancellationToken);

    return result.ToNoContentResult();
  }

  private static async Task<IResult> CreateCardSubtask(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardSubtaskRequest request,
    CurrentUser user,
    IBoardCollaborationService boardCollaboration,
    CancellationToken cancellationToken)
  {
    var result = await boardCollaboration.CreateCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> UpdateCardSubtask(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    UpdateBoardCardSubtaskRequest request,
    CurrentUser user,
    IBoardCollaborationService boardCollaboration,
    CancellationToken cancellationToken)
  {
    var result = await boardCollaboration.UpdateCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      subtaskId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> DeleteCardSubtask(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    CurrentUser user,
    IBoardCollaborationService boardCollaboration,
    CancellationToken cancellationToken)
  {
    var result = await boardCollaboration.DeleteCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      subtaskId,
      user.Id,
      cancellationToken);

    return result.ToNoContentResult();
  }

  private static async Task<IResult> AddCardDependency(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    AddBoardCardDependencyRequest request,
    CurrentUser user,
    IBoardDependencyService boardDependencies,
    CancellationToken cancellationToken)
  {
    var result = await boardDependencies.AddCardDependencyAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> DeleteCardDependency(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    CurrentUser user,
    IBoardDependencyService boardDependencies,
    CancellationToken cancellationToken)
  {
    var result = await boardDependencies.DeleteCardDependencyAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      dependsOnCardId,
      user.Id,
      cancellationToken);

    return result.ToNoContentResult();
  }

  private static async Task<IResult> DeleteCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CurrentUser user,
    IBoardCardService boardCards,
    CancellationToken cancellationToken)
  {
    var result = await boardCards.DeleteCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      user.Id,
      cancellationToken);

    return result.ToNoContentResult();
  }
}
