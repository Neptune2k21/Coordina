using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

public sealed class BoardCollaborationService(
  IBoardStore boards,
  IProjectAccessGuard projectAccess) : IBoardCollaborationService
{
  public async Task<BoardResult<BoardResponse>> AddCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardCommentRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != ProjectAccessStatus.Granted)
    {
      return BoardResults.AccessFailure<BoardResponse>(access.Status);
    }

    var body = BoardRules.NormalizeText(request.Body);
    if (string.IsNullOrWhiteSpace(body))
    {
      return new BoardResult<BoardResponse>(
        BoardResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.Body)] = ["Comment is required."]
        });
    }

    if (body.Length > 1000)
    {
      return new BoardResult<BoardResponse>(
        BoardResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.Body)] = ["Comment must be 1000 characters or fewer."]
        });
    }

    var board = await boards.AddCardCommentAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      userId,
      body,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new BoardResult<BoardResponse>(BoardResultStatus.NotFound)
      : new BoardResult<BoardResponse>(
        BoardResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<BoardResult<BoardResponse>> CreateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardSubtaskRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != ProjectAccessStatus.Granted)
    {
      return BoardResults.AccessFailure<BoardResponse>(access.Status);
    }

    var titleErrors = BoardRules.ValidateCard(
      request.Title,
      null,
      null,
      [],
      null);

    if (titleErrors.Count > 0)
    {
      return new BoardResult<BoardResponse>(
        BoardResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.Title)] = titleErrors["Title"]
        });
    }

    var board = await boards.CreateCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request.Title!.Trim(),
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new BoardResult<BoardResponse>(BoardResultStatus.NotFound)
      : new BoardResult<BoardResponse>(
        BoardResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<BoardResult<BoardResponse>> UpdateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    UpdateBoardCardSubtaskRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != ProjectAccessStatus.Granted)
    {
      return BoardResults.AccessFailure<BoardResponse>(access.Status);
    }

    if (request.Title is not null)
    {
      var errors = BoardRules.ValidateCard(
        request.Title,
        null,
        null,
        [],
        null);

      if (errors.Count > 0)
      {
        return new BoardResult<BoardResponse>(
          BoardResultStatus.ValidationError,
          Errors: new Dictionary<string, string[]>
          {
            [nameof(request.Title)] = errors["Title"]
          });
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
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new BoardResult<BoardResponse>(BoardResultStatus.NotFound)
      : new BoardResult<BoardResponse>(
        BoardResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<BoardResult<object>> DeleteCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != ProjectAccessStatus.Granted)
    {
      return BoardResults.AccessFailure<object>(access.Status);
    }

    var deleted = await boards.DeleteCardSubtaskAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      subtaskId,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return deleted
      ? new BoardResult<object>(BoardResultStatus.Success)
      : new BoardResult<object>(BoardResultStatus.NotFound);
  }
}
