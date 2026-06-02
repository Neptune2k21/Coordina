using System.Security.Claims;
using Coordina.Api.Modules.Tasks.Application;
using Coordina.Api.Modules.Tasks.Contracts;

namespace Coordina.Api.Modules.Tasks;

public static class BoardEndpoints
{
  public static IEndpointRouteBuilder MapBoardEndpoints(
    this IEndpointRouteBuilder app)
  {
    var projectGroup = app
      .MapGroup("/workspaces/{workspaceId:guid}/projects/{projectId:guid}")
      .RequireAuthorization()
      .WithTags("Boards");

    projectGroup.MapGet("/board", GetDefault)
      .WithSummary("Get default project board")
      .WithDescription("Returns a full board with lists, cards, labels, and assignees.");

    projectGroup.MapPost("/boards", CreateBoard)
      .WithSummary("Create board")
      .WithDescription("Creates a project board and generates lists from the selected template.");

    var boardGroup = projectGroup.MapGroup("/boards/{boardId:guid}");

    boardGroup.MapPost("/lists", CreateList)
      .WithSummary("Create board list");

    boardGroup.MapPatch("/lists/{listId:guid}", UpdateList)
      .WithSummary("Update board list title");

    boardGroup.MapPost("/lists/{listId:guid}/cards", CreateCard)
      .WithSummary("Create card");

    boardGroup.MapPatch("/cards/{cardId:guid}", UpdateCard)
      .WithSummary("Update card");

    boardGroup.MapPatch("/cards/{cardId:guid}/move", MoveCard)
      .WithSummary("Move card");

    boardGroup.MapPost("/cards/{cardId:guid}/comments", AddCardComment)
      .WithSummary("Add card comment");

    boardGroup.MapPost("/cards/{cardId:guid}/subtasks", CreateCardSubtask)
      .WithSummary("Create card subtask");

    boardGroup.MapPatch("/cards/{cardId:guid}/subtasks/{subtaskId:guid}", UpdateCardSubtask)
      .WithSummary("Update card subtask");

    boardGroup.MapDelete("/cards/{cardId:guid}/subtasks/{subtaskId:guid}", DeleteCardSubtask)
      .WithSummary("Delete card subtask");

    boardGroup.MapPost("/cards/{cardId:guid}/dependencies", AddCardDependency)
      .WithSummary("Add card dependency");

    boardGroup.MapDelete("/cards/{cardId:guid}/dependencies/{dependsOnCardId:guid}", DeleteCardDependency)
      .WithSummary("Delete card dependency");

    boardGroup.MapDelete("/cards/{cardId:guid}", DeleteCard)
      .WithSummary("Delete card");

    return app;
  }

  private static async Task<IResult> GetDefault(
    Guid workspaceId,
    Guid projectId,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.GetDefaultAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> CreateBoard(
    Guid workspaceId,
    Guid projectId,
    CreateBoardRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.CreateAsync(
      workspaceId,
      projectId,
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      TaskResultStatus.Success => Results.Created(
        $"/workspaces/{workspaceId}/projects/{projectId}/boards/{result.Value!.Id}",
        result.Value),
      _ => ToResult(result)
    };
  }

  private static async Task<IResult> CreateList(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CreateBoardListRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.CreateListAsync(
      workspaceId,
      projectId,
      boardId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> UpdateList(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    UpdateBoardListRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.UpdateListAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> CreateCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    CreateBoardCardRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.CreateCardAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> UpdateCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    UpdateBoardCardRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.UpdateCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> MoveCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    MoveBoardCardRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.MoveCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> AddCardComment(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardCommentRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.AddCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> CreateCardSubtask(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardSubtaskRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.CreateCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> UpdateCardSubtask(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    UpdateBoardCardSubtaskRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.UpdateCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      subtaskId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> DeleteCardSubtask(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.DeleteCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      subtaskId,
      userId,
      cancellationToken);

    return ToEmptyResult(result);
  }

  private static async Task<IResult> AddCardDependency(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    AddBoardCardDependencyRequest request,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.AddCardDependencyAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request,
      userId,
      cancellationToken);

    return ToResult(result);
  }

  private static async Task<IResult> DeleteCardDependency(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.DeleteCardDependencyAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      dependsOnCardId,
      userId,
      cancellationToken);

    return ToEmptyResult(result);
  }

  private static async Task<IResult> DeleteCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    ClaimsPrincipal user,
    IBoardService boardService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await boardService.DeleteCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      userId,
      cancellationToken);

    return ToEmptyResult(result);
  }

  private static IResult ToEmptyResult<T>(TaskResult<T> result)
  {
    return result.Status switch
    {
      TaskResultStatus.Success => Results.NoContent(),
      TaskResultStatus.Forbidden => Results.Forbid(),
      TaskResultStatus.NotFound => Results.NotFound(),
      TaskResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static IResult ToResult<T>(TaskResult<T> result)
  {
    return result.Status switch
    {
      TaskResultStatus.Success => Results.Ok(result.Value),
      TaskResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      TaskResultStatus.Forbidden => Results.Forbid(),
      TaskResultStatus.NotFound => Results.NotFound(),
      TaskResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static bool TryGetUserId(
    ClaimsPrincipal user,
    out Guid userId)
  {
    return Guid.TryParse(
      user.FindFirstValue(ClaimTypes.NameIdentifier),
      out userId);
  }
}
