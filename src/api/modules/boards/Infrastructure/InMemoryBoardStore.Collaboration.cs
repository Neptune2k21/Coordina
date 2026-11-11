using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Comments and subtasks on a card.
public sealed partial class InMemoryBoardStore
{
  public Task<ProjectBoard?> AddCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid userId,
    string body,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetCard(workspaceId, projectId, boardId, cardId, out var card))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var comment = new CommentRecord(
      Guid.NewGuid(),
      cardId,
      userId,
      body,
      createdAt);
    _comments[comment.Id] = comment;
    return TouchedWithCardAsync(board, card, createdAt);
  }

  public Task<ProjectBoardCardComment?> FindCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return Task.FromResult(
      TryGetCard(workspaceId, projectId, boardId, cardId, out _)
      && _comments.TryGetValue(commentId, out var comment)
      && comment.CardId == cardId
        ? new ProjectBoardCardComment(
          comment.Id,
          comment.CardId,
          new ProjectBoardCardUser(comment.UserId, null, null),
          comment.Body,
          comment.CreatedAt,
          comment.EditedAt)
        : null);
  }

  public Task<ProjectBoard?> UpdateCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    string body,
    DateTimeOffset editedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetCard(workspaceId, projectId, boardId, cardId, out var card)
      || !_comments.TryGetValue(commentId, out var comment)
      || comment.CardId != cardId)
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    _comments[commentId] = comment with { Body = body, EditedAt = editedAt };
    return TouchedWithCardAsync(board, card, editedAt);
  }

  public Task<bool> DeleteCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid commentId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetCard(workspaceId, projectId, boardId, cardId, out var card)
      || !_comments.TryGetValue(commentId, out var comment)
      || comment.CardId != cardId
      || !_comments.TryRemove(commentId, out _))
    {
      return Task.FromResult(false);
    }

    _cards[cardId] = card with { UpdatedAt = updatedAt };
    _boards[boardId] = board with { UpdatedAt = updatedAt };

    return Task.FromResult(true);
  }

  public Task<ProjectBoard?> CreateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    string title,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetCard(workspaceId, projectId, boardId, cardId, out var card))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var position = _subtasks.Values
      .Where(subtask => subtask.CardId == cardId)
      .Select(subtask => (int?)subtask.Position)
      .Max() + 1 ?? 0;
    var subtask = new SubtaskRecord(
      Guid.NewGuid(),
      cardId,
      title,
      false,
      null,
      null,
      position,
      createdAt,
      createdAt);
    _subtasks[subtask.Id] = subtask;
    return TouchedWithCardAsync(board, card, createdAt);
  }

  public Task<ProjectBoard?> UpdateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    string? title,
    bool? isCompleted,
    Guid userId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetSubtask(workspaceId, projectId, boardId, cardId, subtaskId, out var board, out var card, out var subtask))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var nextIsCompleted = isCompleted ?? subtask.IsCompleted;
    _subtasks[subtaskId] = subtask with
    {
      Title = title ?? subtask.Title,
      IsCompleted = nextIsCompleted,
      CompletedAt = nextIsCompleted
        ? subtask.CompletedAt ?? updatedAt
        : null,
      CompletedByUserId = nextIsCompleted
        ? subtask.CompletedByUserId ?? userId
        : null,
      UpdatedAt = updatedAt
    };
    return TouchedWithCardAsync(board, card, updatedAt);
  }

  public Task<bool> DeleteCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetSubtask(workspaceId, projectId, boardId, cardId, subtaskId, out var board, out var card, out var subtask))
    {
      return Task.FromResult(false);
    }

    var deleted = _subtasks.TryRemove(subtaskId, out _);
    if (deleted)
    {
      _cards[cardId] = card with { UpdatedAt = updatedAt };
      _boards[boardId] = board with { UpdatedAt = updatedAt };
    }

    return Task.FromResult(deleted);
  }
}
