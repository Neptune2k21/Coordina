using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardCardService
{
  Task<BoardResult<BoardResponse>> CreateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    CreateBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<BoardResult<BoardResponse>> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    UpdateBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<BoardResult<BoardResponse>> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    MoveBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<BoardResult<object>> DeleteCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid userId,
    CancellationToken cancellationToken);
}
