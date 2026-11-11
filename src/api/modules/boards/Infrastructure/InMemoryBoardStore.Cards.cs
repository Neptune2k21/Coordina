using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Creating, changing, moving and deleting cards.
public sealed partial class InMemoryBoardStore
{
  public Task<ProjectBoardCard?> FindCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_cards.TryGetValue(cardId, out var card)
      || card.WorkspaceId != workspaceId
      || card.ProjectId != projectId
      || card.BoardId != boardId)
    {
      return Task.FromResult<ProjectBoardCard?>(null);
    }

    return Task.FromResult<ProjectBoardCard?>(ToCard(card));
  }

  public Task<ProjectBoard?> CreateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetList(workspaceId, projectId, boardId, listId, out _))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var position = _cards.Values
      .Where(card => card.BoardId == boardId && card.ListId == listId)
      .Select(card => (int?)card.Position)
      .Max() + 1 ?? 0;
    var card = new CardRecord(
      Guid.NewGuid(),
      boardId,
      listId,
      projectId,
      workspaceId,
      mutation.Title,
      mutation.Description,
      mutation.Priority,
      mutation.DueDate,
      mutation.EstimatedMinutes,
      mutation.Labels.ToArray(),
      mutation.IsCompleted,
      mutation.CompletedAt,
      mutation.CompletedByUserId,
      position,
      createdAt,
      createdAt,
      mutation.AssigneeIds.ToArray());
    _cards[card.Id] = card;
    return TouchedAsync(board, createdAt);
  }

  public Task<ProjectBoard?> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetCardInList(workspaceId, projectId, boardId, cardId, listId, out var board, out var card))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    _cards[cardId] = card with
    {
      ListId = listId,
      Position = card.ListId == listId
        ? card.Position
        : NextCardPosition(boardId, listId),
      Title = mutation.Title,
      Description = mutation.Description,
      Priority = mutation.Priority,
      DueDate = mutation.DueDate,
      EstimatedMinutes = mutation.EstimatedMinutes,
      Labels = mutation.Labels.ToArray(),
      IsCompleted = mutation.IsCompleted,
      CompletedAt = mutation.CompletedAt,
      CompletedByUserId = mutation.CompletedByUserId,
      UpdatedAt = updatedAt,
      AssigneeIds = mutation.AssigneeIds.ToArray()
    };
    return TouchedAsync(board, updatedAt);
  }

  public Task<ProjectBoard?> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardCompletion completion,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetCardInList(workspaceId, projectId, boardId, cardId, listId, out var board, out var card))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    _cards[cardId] = card with
    {
      ListId = listId,
      Position = NextCardPosition(boardId, listId),
      IsCompleted = completion.IsCompleted,
      CompletedAt = completion.CompletedAt,
      CompletedByUserId = completion.CompletedByUserId,
      UpdatedAt = updatedAt
    };
    return TouchedAsync(board, updatedAt);
  }

  public Task UnassignFromWorkspaceAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    foreach (var card in _cards.Values
      .Where(card => card.WorkspaceId == workspaceId && card.AssigneeIds.Contains(userId))
      .ToArray())
    {
      _cards[card.Id] = card with
      {
        AssigneeIds = card.AssigneeIds.Where(assigneeId => assigneeId != userId).ToArray()
      };
    }

    return Task.CompletedTask;
  }

  public Task<bool> DeleteCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_cards.TryGetValue(cardId, out var card)
      || card.WorkspaceId != workspaceId
      || card.ProjectId != projectId
      || card.BoardId != boardId)
    {
      return Task.FromResult(false);
    }

    foreach (var dependency in _dependencies.Values
      .Where(dependency => dependency.CardId == cardId
        || dependency.DependsOnCardId == cardId)
      .ToArray())
    {
      _dependencies.TryRemove(dependency.Id, out _);
    }

    foreach (var comment in _comments.Values
      .Where(comment => comment.CardId == cardId)
      .ToArray())
    {
      _comments.TryRemove(comment.Id, out _);
    }

    foreach (var subtask in _subtasks.Values
      .Where(subtask => subtask.CardId == cardId)
      .ToArray())
    {
      _subtasks.TryRemove(subtask.Id, out _);
    }

    return Task.FromResult(_cards.TryRemove(cardId, out _));
  }
}
