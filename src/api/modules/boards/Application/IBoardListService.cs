using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardListService
{
  Task<Result<BoardResponse>> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CreateBoardListRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<Result<BoardResponse>> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    UpdateBoardListRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  /// <summary>
  /// Deletes a list. Its cards move to <paramref name="moveCardsToListId"/>,
  /// which is required when it has any.
  /// </summary>
  Task<Result<BoardResponse>> DeleteListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    Guid? moveCardsToListId,
    Guid userId,
    CancellationToken cancellationToken);
}
