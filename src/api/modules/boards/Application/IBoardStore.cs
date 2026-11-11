using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardStore
{
  /// <summary>
  /// Runs <paramref name="action"/> as one transaction while no other caller
  /// holds this lock for the board, so a check and the write that depends on
  /// it cannot interleave with another request.
  /// </summary>
  Task<T> RunExclusiveAsync<T>(
    Guid boardId,
    Func<CancellationToken, Task<T>> action,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> FindDefaultForProjectAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> FindInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CancellationToken cancellationToken);

  /// <summary>Returns <c>null</c> when the project already has a board.</summary>
  Task<ProjectBoard?> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    string name,
    BoardTemplate boardTemplate,
    IReadOnlyCollection<BoardListSeed> listSeeds,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    string title,
    BoardTaskStatus taskStatus,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  /// <summary>
  /// Applies <paramref name="update"/> and moves the list to its position,
  /// renumbering the others. Returns <c>null</c> when the list is not found.
  /// </summary>
  Task<ProjectBoard?> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardListUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  /// <summary>
  /// Deletes the list, after moving its cards with <paramref name="transfer"/>
  /// when it has any. Returns <c>null</c> when the list is not found.
  /// </summary>
  Task<ProjectBoard?> DeleteListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardTransfer? transfer,
    DateTimeOffset deletedAt,
    CancellationToken cancellationToken);

  Task<ProjectBoardCard?> FindCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> CreateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardCompletion completion,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> AddCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid userId,
    string body,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  /// <summary>The comment, when it belongs to that card of that board; its author has only an id.</summary>
  Task<ProjectBoardCardComment?> FindCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> UpdateCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    string body,
    DateTimeOffset editedAt,
    CancellationToken cancellationToken);

  Task<bool> DeleteCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> CreateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    string title,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> UpdateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    string? title,
    bool? isCompleted,
    Guid userId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<bool> DeleteCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<ProjectBoard?> AddCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  Task<bool> DeleteCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<bool> DeleteCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken);
}

public sealed record BoardCardMutation(
  string Title,
  string? Description,
  BoardCardPriority? Priority,
  DateOnly? DueDate,
  int? EstimatedMinutes,
  IReadOnlyCollection<string> Labels,
  IReadOnlyCollection<Guid> AssigneeIds,
  bool IsCompleted,
  DateTimeOffset? CompletedAt,
  Guid? CompletedByUserId);

public sealed record BoardCardCompletion(
  bool IsCompleted,
  DateTimeOffset? CompletedAt,
  Guid? CompletedByUserId)
{
  public static BoardCardCompletion Open { get; } = new(false, null, null);
}

/// <summary>
/// The new state of a list. <see cref="CardCompletion"/>, when set, is given
/// to the list's cards whose completion differs from it, as moving them into
/// a list with the new status would.
/// </summary>
public sealed record BoardListUpdate(
  string Title,
  BoardTaskStatus TaskStatus,
  int Position,
  BoardCardCompletion? CardCompletion);

/// <summary>
/// Where a deleted list's cards go. They keep their order at the end of the
/// target list, and <see cref="Completion"/> is given to those whose
/// completion differs from it.
/// </summary>
public sealed record BoardCardTransfer(
  Guid TargetListId,
  BoardCardCompletion Completion);

public sealed record BoardListSeed(
  string Title,
  BoardTaskStatus TaskStatus,
  IReadOnlyCollection<BoardCardSeed> Cards);

public sealed record BoardCardSeed(
  Guid Id,
  string Title,
  string? Description,
  BoardCardPriority? Priority,
  DateOnly? DueDate,
  int? EstimatedMinutes,
  IReadOnlyCollection<string> Labels,
  IReadOnlyCollection<string> Subtasks,
  IReadOnlyCollection<Guid> DependsOnCardIds);
