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

  Task<TaskResult<BoardGraphResponse>> GetGraphAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardCardDependencyAnalysisResponse>> GetCardDependencyAnalysisAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
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

  Task<TaskResult<BoardResponse>> AddCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardCommentRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> CreateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CreateBoardCardSubtaskRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> UpdateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    UpdateBoardCardSubtaskRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<object>> DeleteCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<BoardResponse>> AddCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    AddBoardCardDependencyRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<object>> DeleteCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
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
