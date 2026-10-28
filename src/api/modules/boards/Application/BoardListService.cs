using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;

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

    var errors = BoardRules.ValidateList(request.Title);
    var taskStatus = BoardRules.ParseListStatus(
      request.TaskStatus,
      out var statusError);

    if (statusError is not null)
    {
      errors.Add("TaskStatus", statusError);
    }

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var board = await boards.CreateListAsync(
      workspaceId,
      projectId,
      boardId,
      request.Title!.Trim(),
      taskStatus,
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
    var access = await projectAccess.AuthorizeWriteAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    var errors = BoardRules.ValidateList(request.Title);

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var board = await boards.UpdateListAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      request.Title!.Trim(),
      clock.GetUtcNow(),
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }
}
