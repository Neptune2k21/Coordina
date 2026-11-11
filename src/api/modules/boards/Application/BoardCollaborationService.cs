using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Workspaces.Domain;
using Coordina.Api.Shared.Text;
using Coordina.Api.Shared.Validation;

namespace Coordina.Api.Modules.Boards.Application;

public sealed class BoardCollaborationService(
  IBoardStore boards,
  IProjectAccessGuard projectAccess,
  TimeProvider clock) : IBoardCollaborationService
{
  public async Task<Result<BoardResponse>> AddCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardCommentRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var body = await AuthorizeCommentTextAsync(
      workspaceId,
      projectId,
      userId,
      request.Body,
      cancellationToken);

    if (!body.IsSuccess)
    {
      return body.Error;
    }

    var board = await boards.AddCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      userId,
      body.Value,
      clock.GetUtcNow(),
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  public async Task<Result<BoardResponse>> UpdateCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    UpdateBoardCardCommentRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var body = await AuthorizeCommentTextAsync(
      workspaceId,
      projectId,
      userId,
      request.Body,
      cancellationToken);

    if (!body.IsSuccess)
    {
      return body.Error;
    }

    var comment = await boards.FindCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      commentId,
      cancellationToken);

    if (comment is null)
    {
      return Error.NotFound();
    }

    // Words keep meaning what their author wrote, so nobody else edits them.
    if (comment.Author.UserId != userId)
    {
      return Error.Forbidden("Only the author can edit a comment.");
    }

    var board = await boards.UpdateCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      commentId,
      body.Value,
      clock.GetUtcNow(),
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  public async Task<Result> DeleteCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
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

    var comment = await boards.FindCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      commentId,
      cancellationToken);

    if (comment is null)
    {
      return Error.NotFound();
    }

    // Owners can take down a comment posted on the wrong card or worse.
    if (comment.Author.UserId != userId && access.Value.Role != WorkspaceRole.Owner)
    {
      return Error.Forbidden("Only the author or a workspace owner can delete a comment.");
    }

    var deleted = await boards.DeleteCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      commentId,
      clock.GetUtcNow(),
      cancellationToken);

    return deleted ? Result.Success() : Error.NotFound();
  }

  public async Task<Result<BoardResponse>> CreateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardSubtaskRequest request,
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

    var errors = BoardRules.ValidateCardTitle(request.Title);

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var board = await boards.CreateCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request.Title!.Trim(),
      clock.GetUtcNow(),
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  public async Task<Result<BoardResponse>> UpdateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    UpdateBoardCardSubtaskRequest request,
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

    if (request.Title is not null)
    {
      var errors = BoardRules.ValidateCardTitle(request.Title);

      if (errors.HasErrors)
      {
        return errors.ToError();
      }
    }

    var board = await boards.UpdateCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      subtaskId,
      request.Title?.Trim(),
      request.IsCompleted,
      userId,
      clock.GetUtcNow(),
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  public async Task<Result> DeleteCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
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

    var deleted = await boards.DeleteCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      subtaskId,
      clock.GetUtcNow(),
      cancellationToken);

    return deleted ? Result.Success() : Error.NotFound();
  }

  /// <summary>Checks write access to the project, then returns the comment text trimmed and validated.</summary>
  private async Task<Result<string>> AuthorizeCommentTextAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    string? text,
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

    var body = TextNormalizer.TrimToNull(text);
    var errors = new ValidationErrors().Required("Body", body, "Comment", maxLength: 1000);

    return errors.HasErrors ? errors.ToError() : Result.Success(body!);
  }
}
