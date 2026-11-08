using Coordina.Api.Modules.Boards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Comments and subtasks on a card.
public sealed partial class PostgresBoardStore
{
  public async Task<ProjectBoard?> AddCardCommentAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid userId,
    string body,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var cardExists = await CardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);

    if (!cardExists)
    {
      return null;
    }

    BoardCardComments.Add(new BoardCardCommentEntity
    {
      Id = Guid.NewGuid(),
      CardId = cardId,
      UserId = userId,
      Body = body,
      CreatedAt = createdAt
    });

    await TouchCardAsync(cardId, createdAt, cancellationToken);
    await TouchBoardAsync(boardId, createdAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  // Positions are MAX + 1, so allocating one must not interleave with another write.
  public Task<ProjectBoard?> CreateCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    string title,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken) =>
    RunExclusiveAsync(
      boardId,
      token => CreateCardSubtaskUnderLockAsync(
        workspaceId,
        projectId,
        boardId,
        cardId,
        title,
        createdAt,
        token),
      cancellationToken);

  private async Task<ProjectBoard?> CreateCardSubtaskUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    string title,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var cardExists = await CardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);

    if (!cardExists)
    {
      return null;
    }

    var nextPosition = await BoardCardSubtasks
      .Where(subtask => subtask.CardId == cardId)
      .Select(subtask => (int?)subtask.Position)
      .MaxAsync(cancellationToken) + 1 ?? 0;

    BoardCardSubtasks.Add(new BoardCardSubtaskEntity
    {
      Id = Guid.NewGuid(),
      CardId = cardId,
      Title = title,
      IsCompleted = false,
      Position = nextPosition,
      CreatedAt = createdAt,
      UpdatedAt = createdAt
    });

    await TouchCardAsync(cardId, createdAt, cancellationToken);
    await TouchBoardAsync(boardId, createdAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<ProjectBoard?> UpdateCardSubtaskAsync(
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
    var cardExists = await CardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);

    if (!cardExists)
    {
      return null;
    }

    var subtask = await BoardCardSubtasks
      .SingleOrDefaultAsync(subtask => subtask.CardId == cardId
        && subtask.Id == subtaskId,
        cancellationToken);

    if (subtask is null)
    {
      return null;
    }

    if (title is not null)
    {
      subtask.Title = title;
    }

    if (isCompleted is not null && subtask.IsCompleted != isCompleted.Value)
    {
      subtask.IsCompleted = isCompleted.Value;
      subtask.CompletedAt = isCompleted.Value ? updatedAt : null;
      subtask.CompletedByUserId = isCompleted.Value ? userId : null;
    }

    subtask.UpdatedAt = updatedAt;

    await TouchCardAsync(cardId, updatedAt, cancellationToken);
    await TouchBoardAsync(boardId, updatedAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<bool> DeleteCardSubtaskAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var cardExists = await CardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);

    if (!cardExists)
    {
      return false;
    }

    var deleted = await BoardCardSubtasks
      .Where(subtask => subtask.CardId == cardId && subtask.Id == subtaskId)
      .ExecuteDeleteAsync(cancellationToken);

    if (deleted == 0)
    {
      return false;
    }

    await TouchCardAsync(cardId, updatedAt, cancellationToken);
    await TouchBoardAsync(boardId, updatedAt, cancellationToken);

    return true;
  }
}
