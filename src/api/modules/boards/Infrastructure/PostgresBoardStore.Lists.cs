using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Creating, changing and deleting lists, and the positions they share.
public sealed partial class PostgresBoardStore
{
  // Positions are MAX + 1, so allocating one must not interleave with another write.
  public Task<ProjectBoard?> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    string title,
    BoardTaskStatus taskStatus,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken) =>
    RunExclusiveAsync(
      boardId,
      token => CreateListUnderLockAsync(
        workspaceId,
        projectId,
        boardId,
        title,
        taskStatus,
        createdAt,
        token),
      cancellationToken);

  private async Task<ProjectBoard?> CreateListUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    string title,
    BoardTaskStatus taskStatus,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var boardExists = await BoardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);

    if (!boardExists)
    {
      return null;
    }

    var nextPosition = await BoardLists
      .Where(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId)
      .Select(list => (int?)list.Position)
      .MaxAsync(cancellationToken) + 1 ?? 0;

    BoardLists.Add(new BoardListEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      ProjectId = projectId,
      BoardId = boardId,
      Title = title,
      Position = nextPosition,
      TaskStatus = taskStatus,
      CreatedAt = createdAt,
      UpdatedAt = createdAt
    });

    await TouchBoardAsync(boardId, createdAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  // Renumbering positions must not interleave with another write.
  public Task<ProjectBoard?> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardListUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken) =>
    RunExclusiveAsync(
      boardId,
      token => UpdateListUnderLockAsync(
        workspaceId,
        projectId,
        boardId,
        listId,
        update,
        updatedAt,
        token),
      cancellationToken);

  private async Task<ProjectBoard?> UpdateListUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardListUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(update);

    var lists = await OrderedListsAsync(workspaceId, projectId, boardId, cancellationToken);
    var list = lists.SingleOrDefault(candidate => candidate.Id == listId);

    if (list is null)
    {
      return null;
    }

    list.Title = update.Title;
    list.TaskStatus = update.TaskStatus;
    list.UpdatedAt = updatedAt;
    lists.Remove(list);
    lists.Insert(Math.Clamp(update.Position, 0, lists.Count), list);
    Renumber(lists);

    if (update.CardCompletion is { } completion)
    {
      await ApplyCompletionAsync(listId, completion, updatedAt, cancellationToken);
    }

    await TouchBoardAsync(boardId, updatedAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  // Card and list positions are renumbered, so this must not interleave with another write.
  public Task<ProjectBoard?> DeleteListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardTransfer? transfer,
    DateTimeOffset deletedAt,
    CancellationToken cancellationToken) =>
    RunExclusiveAsync(
      boardId,
      token => DeleteListUnderLockAsync(
        workspaceId,
        projectId,
        boardId,
        listId,
        transfer,
        deletedAt,
        token),
      cancellationToken);

  private async Task<ProjectBoard?> DeleteListUnderLockAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardTransfer? transfer,
    DateTimeOffset deletedAt,
    CancellationToken cancellationToken)
  {
    var lists = await OrderedListsAsync(workspaceId, projectId, boardId, cancellationToken);
    var list = lists.SingleOrDefault(candidate => candidate.Id == listId);

    if (list is null
      || (transfer is not null && lists.All(candidate => candidate.Id != transfer.TargetListId)))
    {
      return null;
    }

    var cards = await BoardCards
      .Where(card => card.BoardId == boardId && card.ListId == listId)
      .OrderBy(card => card.Position)
      .ToListAsync(cancellationToken);

    if (cards.Count > 0 && transfer is null)
    {
      throw new InvalidOperationException("A list with cards needs a target list for them.");
    }

    if (transfer is not null)
    {
      var position = await NextCardPositionAsync(
        workspaceId,
        projectId,
        boardId,
        transfer.TargetListId,
        cancellationToken);

      foreach (var card in cards)
      {
        card.ListId = transfer.TargetListId;
        card.Position = position++;
        card.UpdatedAt = deletedAt;

        if (card.IsCompleted != transfer.Completion.IsCompleted)
        {
          card.IsCompleted = transfer.Completion.IsCompleted;
          card.CompletedAt = transfer.Completion.CompletedAt;
          card.CompletedByUserId = transfer.Completion.CompletedByUserId;
        }
      }

      // The cards must reach their new list before the cascade from the old one.
      await _dbContext.SaveChangesAsync(cancellationToken);
    }

    BoardLists.Remove(list);
    lists.Remove(list);
    Renumber(lists);
    await TouchBoardAsync(boardId, deletedAt, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  private Task<List<BoardListEntity>> OrderedListsAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CancellationToken cancellationToken) =>
    BoardLists
      .Where(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId)
      .OrderBy(list => list.Position)
      .ThenBy(list => list.CreatedAt)
      .ToListAsync(cancellationToken);

  private static void Renumber(List<BoardListEntity> lists)
  {
    for (var position = 0; position < lists.Count; position++)
    {
      lists[position].Position = position;
    }
  }

  private async Task ApplyCompletionAsync(
    Guid listId,
    BoardCardCompletion completion,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    await BoardCards
      .Where(card => card.ListId == listId && card.IsCompleted != completion.IsCompleted)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(card => card.IsCompleted, completion.IsCompleted)
        .SetProperty(card => card.CompletedAt, completion.CompletedAt)
        .SetProperty(card => card.CompletedByUserId, completion.CompletedByUserId)
        .SetProperty(card => card.UpdatedAt, updatedAt),
        cancellationToken);
  }
}
