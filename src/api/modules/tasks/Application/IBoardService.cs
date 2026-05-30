using Coordina.Api.Modules.Tasks.Contracts;

namespace Coordina.Api.Modules.Tasks.Application;

public interface IBoardService
{
  Task<TaskResult<BoardResponse>> GetDefaultAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    CreateBoardRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CreateBoardListRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    UpdateBoardListRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> CreateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    CreateBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    UpdateBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    MoveBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<object>> DeleteCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid userId,
    CancellationToken cancellationToken);
}
