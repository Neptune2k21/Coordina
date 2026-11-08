using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Shared.Validation;

namespace Coordina.Api.Modules.Boards.Application;

public sealed class BoardListService(
  IBoardStore boards,
  IProjectAccessGuard projectAccess,
  TimeProvider clock) : IBoardListService
{
  public async Task<Result<BoardResponse>> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CreateBoardListRequest request,
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

    var errors = BoardRules.ValidateList(request.Title)
      .Code<BoardTaskStatus>("TaskStatus", request.TaskStatus, "List task status", out var taskStatus);

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var board = await boards.CreateListAsync(
      workspaceId,
      projectId,
      boardId,
      request.Title!.Trim(),
      taskStatus ?? BoardTaskStatus.Todo,
      clock.GetUtcNow(),
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  public async Task<Result<BoardResponse>> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    UpdateBoardListRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var access = await projectAccess.AuthorizeWriteAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    var errors = (request.Title is null ? new ValidationErrors() : BoardRules.ValidateList(request.Title))
      .Code<BoardTaskStatus>("TaskStatus", request.TaskStatus, "List task status", out var taskStatus)
      .AddIf(request.Position < 0, "Position", "Position must be 0 or more.")
      .AddIf(
        request is { Title: null, TaskStatus: null, Position: null },
        "Title",
        "Change the title, the task status or the position.");

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    // The checks read the board, so they and the write must not interleave
    // with another change to its lists.
    return await boards.RunExclusiveAsync(
      boardId,
      token => UpdateListUnderLockAsync(workspaceId, projectId, boardId, listId, request, taskStatus, userId, token),
      cancellationToken);
  }

  public async Task<Result<BoardResponse>> DeleteListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    Guid? moveCardsToListId,
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

    return await boards.RunExclusiveAsync(
      boardId,
      token => DeleteListUnderLockAsync(workspaceId, projectId, boardId, listId, moveCardsToListId, userId, token),
      cancellationToken);
  }

  private async Task<Result<BoardResponse>> UpdateListUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    UpdateBoardListRequest request,
    BoardTaskStatus? taskStatus,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var current = await boards.FindInProjectAsync(workspaceId, projectId, boardId, cancellationToken);
    var list = current?.Lists.SingleOrDefault(candidate => candidate.Id == listId);

    if (current is null || list is null)
    {
      return Error.NotFound();
    }

    if (request.Position >= current.Lists.Count)
    {
      return Error.Validation("Position", $"Position must be between 0 and {current.Lists.Count - 1}.");
    }

    var newStatus = taskStatus ?? list.TaskStatus;

    if (newStatus != list.TaskStatus && LastListError(current, list) is { } conflict)
    {
      return conflict;
    }

    var updatedAt = clock.GetUtcNow();
    var board = await boards.UpdateListAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      new BoardListUpdate(
        request.Title?.Trim() ?? list.Title,
        newStatus,
        request.Position ?? PositionOf(current, list),
        newStatus == list.TaskStatus ? null : CompletionFor(newStatus, userId, updatedAt)),
      updatedAt,
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  private async Task<Result<BoardResponse>> DeleteListUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    Guid? moveCardsToListId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var current = await boards.FindInProjectAsync(workspaceId, projectId, boardId, cancellationToken);
    var list = current?.Lists.SingleOrDefault(candidate => candidate.Id == listId);

    if (current is null || list is null)
    {
      return Error.NotFound();
    }

    if (LastListError(current, list) is { } conflict)
    {
      return conflict;
    }

    var deletedAt = clock.GetUtcNow();
    BoardCardTransfer? transfer = null;

    if (list.Cards.Count > 0)
    {
      if (moveCardsToListId is null || moveCardsToListId == listId)
      {
        return Error.Validation(
          "MoveCardsTo",
          "Choose another list of this board to move this list's cards to.");
      }

      var target = current.Lists.SingleOrDefault(candidate => candidate.Id == moveCardsToListId);

      if (target is null)
      {
        return Error.NotFound();
      }

      transfer = new BoardCardTransfer(target.Id, CompletionFor(target.TaskStatus, userId, deletedAt));
    }

    var board = await boards.DeleteListAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      transfer,
      deletedAt,
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  // New and reopened cards go to the first TODO list, and completed cards to
  // the first DONE list, so a board always keeps one of each.
  private static Error? LastListError(ProjectBoard board, ProjectBoardList list)
  {
    if (list.TaskStatus is not (BoardTaskStatus.Todo or BoardTaskStatus.Done)
      || board.Lists.Any(other => other.Id != list.Id && other.TaskStatus == list.TaskStatus))
    {
      return null;
    }

    return Error.Conflict(list.TaskStatus == BoardTaskStatus.Todo
      ? "A board needs a list with status TODO: new and reopened cards go there. Add another one first."
      : "A board needs a list with status DONE: completed cards go there. Add another one first.");
  }

  private static int PositionOf(ProjectBoard board, ProjectBoardList list) =>
    board.Lists
      .OrderBy(candidate => candidate.Position)
      .Select((candidate, index) => (candidate.Id, index))
      .Single(entry => entry.Id == list.Id)
      .index;

  // Like moving a card: entering a DONE list completes it, leaving one reopens it.
  private static BoardCardCompletion CompletionFor(BoardTaskStatus status, Guid userId, DateTimeOffset at) =>
    status == BoardTaskStatus.Done
      ? new BoardCardCompletion(true, at, userId)
      : BoardCardCompletion.Open;
}
