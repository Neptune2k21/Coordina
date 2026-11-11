using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Creating, changing, moving and deleting cards.
public sealed partial class PostgresBoardStore
{
  public async Task<ProjectBoardCard?> FindCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken)
  {
    var board = await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);

    return board?.Lists
      .SelectMany(list => list.Cards)
      .SingleOrDefault(card => card.Id == cardId);
  }

  // Positions are MAX + 1, so allocating one must not interleave with another write.
  public Task<ProjectBoard?> CreateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken) =>
    RunExclusiveAsync(
      boardId,
      token => CreateCardUnderLockAsync(
        workspaceId,
        projectId,
        boardId,
        listId,
        mutation,
        createdAt,
        token),
      cancellationToken);

  private async Task<ProjectBoard?> CreateCardUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var listExists = await ListExistsAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      cancellationToken);

    if (!listExists)
    {
      return null;
    }

    var nextPosition = await BoardCards
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.ListId == listId)
      .Select(card => (int?)card.Position)
      .MaxAsync(cancellationToken) + 1 ?? 0;

    var card = new BoardCardEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      ProjectId = projectId,
      BoardId = boardId,
      ListId = listId,
      Title = mutation.Title,
      Description = mutation.Description,
      Priority = mutation.Priority,
      DueDate = mutation.DueDate,
      Size = mutation.Size,
      Labels = mutation.Labels.ToArray(),
      IsCompleted = mutation.IsCompleted,
      CompletedAt = mutation.CompletedAt,
      CompletedByUserId = mutation.CompletedByUserId,
      Position = nextPosition,
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
      Assignees = mutation.AssigneeIds
        .Select(assigneeId => new BoardCardAssigneeEntity
        {
          Id = Guid.NewGuid(),
          UserId = assigneeId,
          AssignedAt = createdAt
        })
        .ToArray()
    };

    BoardCards.Add(card);
    await TouchBoardAsync(boardId, createdAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  // Positions are MAX + 1, so allocating one must not interleave with another write.
  public Task<ProjectBoard?> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken) =>
    RunExclusiveAsync(
      boardId,
      token => UpdateCardUnderLockAsync(
        workspaceId,
        projectId,
        boardId,
        cardId,
        listId,
        mutation,
        updatedAt,
        token),
      cancellationToken);

  private async Task<ProjectBoard?> UpdateCardUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var card = await BoardCards
      .Include(card => card.Assignees)
      .SingleOrDefaultAsync(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.Id == cardId,
        cancellationToken);

    if (card is null)
    {
      return null;
    }

    if (card.ListId != listId)
    {
      var listExists = await ListExistsAsync(
        workspaceId,
        projectId,
        boardId,
        listId,
        cancellationToken);

      if (!listExists)
      {
        return null;
      }

      card.Position = await NextCardPositionAsync(
        workspaceId,
        projectId,
        boardId,
        listId,
        cancellationToken);
      card.ListId = listId;
    }

    ApplyMutation(card, mutation, updatedAt);
    SyncAssignees(card, mutation.AssigneeIds, updatedAt);

    await TouchBoardAsync(boardId, updatedAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  // Positions are MAX + 1, so allocating one must not interleave with another write.
  public Task<ProjectBoard?> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardCompletion completion,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken) =>
    RunExclusiveAsync(
      boardId,
      token => MoveCardUnderLockAsync(
        workspaceId,
        projectId,
        boardId,
        cardId,
        listId,
        completion,
        updatedAt,
        token),
      cancellationToken);

  private async Task<ProjectBoard?> MoveCardUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    BoardCardCompletion completion,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var listExists = await ListExistsAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      cancellationToken);

    if (!listExists)
    {
      return null;
    }

    var nextPosition = await NextCardPositionAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      cancellationToken);

    var updated = await BoardCards
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.Id == cardId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(card => card.ListId, listId)
        .SetProperty(card => card.Position, nextPosition)
        .SetProperty(card => card.IsCompleted, completion.IsCompleted)
        .SetProperty(card => card.CompletedAt, completion.CompletedAt)
        .SetProperty(card => card.CompletedByUserId, completion.CompletedByUserId)
        .SetProperty(card => card.UpdatedAt, updatedAt),
        cancellationToken);

    if (updated == 0)
    {
      return null;
    }

    await TouchBoardAsync(boardId, updatedAt, cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task UnassignFromWorkspaceAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    await BoardCardAssignees
      .Where(assignee => assignee.UserId == userId
        && assignee.Card!.WorkspaceId == workspaceId)
      .ExecuteDeleteAsync(cancellationToken);
  }

  public async Task<bool> DeleteCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
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

    await BoardCardDependencies
      .Where(dependency => dependency.CardId == cardId
        || dependency.DependsOnCardId == cardId)
      .ExecuteDeleteAsync(cancellationToken);

    var deleted = await BoardCards
      .Where(card => card.Id == cardId)
      .ExecuteDeleteAsync(cancellationToken);

    return deleted > 0;
  }

  private static void ApplyMutation(BoardCardEntity card, BoardCardMutation mutation, DateTimeOffset updatedAt)
  {
    card.Title = mutation.Title;
    card.Description = mutation.Description;
    card.Priority = mutation.Priority;
    card.DueDate = mutation.DueDate;
    card.Size = mutation.Size;
    card.Labels = mutation.Labels.ToArray();
    card.IsCompleted = mutation.IsCompleted;
    card.CompletedAt = mutation.CompletedAt;
    card.CompletedByUserId = mutation.CompletedByUserId;
    card.UpdatedAt = updatedAt;
  }

  // Keeps the assignees that stay, with their original assignment date.
  private void SyncAssignees(
    BoardCardEntity card,
    IReadOnlyCollection<Guid> assigneeIds,
    DateTimeOffset assignedAt)
  {
    var requested = assigneeIds.ToHashSet();
    var existing = card.Assignees.ToArray();
    var existingIds = existing.Select(assignee => assignee.UserId).ToHashSet();

    BoardCardAssignees.RemoveRange(existing.Where(assignee => !requested.Contains(assignee.UserId)));
    BoardCardAssignees.AddRange(
      requested
        .Where(userId => !existingIds.Contains(userId))
        .Select(userId => new BoardCardAssigneeEntity
        {
          Id = Guid.NewGuid(),
          CardId = card.Id,
          UserId = userId,
          AssignedAt = assignedAt
        }));
  }
}
