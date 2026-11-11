using Coordina.Api.Modules.Auth.Infrastructure;
using Coordina.Api.Modules.Boards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Loading a board and mapping its rows to the domain model.
public sealed partial class PostgresBoardStore
{
  public async Task<ProjectBoard?> FindDefaultForProjectAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken)
  {
    var boardId = await Boards
      .AsNoTracking()
      .Where(board => board.WorkspaceId == workspaceId
        && board.ProjectId == projectId)
      .OrderBy(board => board.CreatedAt)
      .Select(board => (Guid?)board.Id)
      .FirstOrDefaultAsync(cancellationToken);

    return boardId is null
      ? null
      : await FindInProjectAsync(
        workspaceId,
        projectId,
        boardId.Value,
        cancellationToken);
  }

  public async Task<ProjectBoard?> FindInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CancellationToken cancellationToken)
  {
    var board = await Boards
      .AsNoTracking()
      .Where(board => board.WorkspaceId == workspaceId
        && board.ProjectId == projectId
        && board.Id == boardId)
      .SingleOrDefaultAsync(cancellationToken);

    if (board is null)
    {
      return null;
    }

    var lists = await BoardLists
      .AsNoTracking()
      .Where(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId)
      .OrderBy(list => list.Position)
      .ThenBy(list => list.CreatedAt)
      .ThenBy(list => list.Id)
      .ToArrayAsync(cancellationToken);
    var cards = await BoardCards
      .AsNoTracking()
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId)
      .OrderBy(card => card.Position)
      .ThenBy(card => card.CreatedAt)
      .ThenBy(card => card.Id)
      .ToArrayAsync(cancellationToken);
    var details = await LoadCardDetailsAsync(cards, cancellationToken);
    var cardsByList = cards.ToLookup(card => card.ListId, details.ToCard);

    return new ProjectBoard(
      board.Id,
      board.ProjectId,
      board.WorkspaceId,
      board.Name,
      board.Template,
      board.CreatedAt,
      board.UpdatedAt,
      lists.Select(list => ToList(list, cardsByList[list.Id].ToArray())).ToArray());
  }

  // Everything a card shows besides its own row, loaded for all the board's
  // cards at once so a board costs the same few queries whatever its size.
  private async Task<CardDetails> LoadCardDetailsAsync(
    BoardCardEntity[] cards,
    CancellationToken cancellationToken)
  {
    var cardIds = cards.Select(card => card.Id).ToArray();
    var subtasks = await BoardCardSubtasks
      .AsNoTracking()
      .Where(subtask => cardIds.Contains(subtask.CardId))
      .OrderBy(subtask => subtask.Position)
      .ThenBy(subtask => subtask.CreatedAt)
      .ThenBy(subtask => subtask.Id)
      .ToArrayAsync(cancellationToken);
    var users = await LoadUsersAsync(
      cards.Select(card => card.CompletedByUserId)
        .Concat(subtasks.Select(subtask => subtask.CompletedByUserId))
        .OfType<Guid>()
        .Distinct()
        .ToArray(),
      cancellationToken);

    return new CardDetails(
      await LoadAssigneesAsync(cardIds, cancellationToken),
      await LoadCommentsAsync(cardIds, cancellationToken),
      subtasks.Select(subtask => ToSubtask(subtask, users)).ToLookup(subtask => subtask.CardId),
      await LoadDependenciesAsync(cards, cancellationToken),
      users);
  }

  private async Task<ILookup<Guid, ProjectBoardCardAssignee>> LoadAssigneesAsync(
    Guid[] cardIds,
    CancellationToken cancellationToken)
  {
    var assignees = await BoardCardAssignees
      .AsNoTracking()
      .Where(assignee => cardIds.Contains(assignee.CardId))
      .Join(
        AuthUsers.AsNoTracking(),
        assignee => assignee.UserId,
        user => user.Id,
        (assignee, user) => new { assignee.CardId, assignee.UserId, user.Name, user.Email })
      .ToArrayAsync(cancellationToken);

    return assignees.ToLookup(
      assignee => assignee.CardId,
      assignee => new ProjectBoardCardAssignee(assignee.UserId, assignee.Name, assignee.Email));
  }

  private async Task<ILookup<Guid, ProjectBoardCardComment>> LoadCommentsAsync(
    Guid[] cardIds,
    CancellationToken cancellationToken)
  {
    var comments = await BoardCardComments
      .AsNoTracking()
      .Where(comment => cardIds.Contains(comment.CardId))
      .Join(
        AuthUsers.AsNoTracking(),
        comment => comment.UserId,
        user => user.Id,
        (comment, user) => new ProjectBoardCardComment(
          comment.Id,
          comment.CardId,
          new ProjectBoardCardUser(user.Id, user.Name, user.Email),
          comment.Body,
          comment.CreatedAt,
          comment.EditedAt))
      .ToArrayAsync(cancellationToken);

    return comments.OrderBy(comment => comment.CreatedAt).ToLookup(comment => comment.CardId);
  }

  // Only dependencies on cards of the same board are shown.
  private async Task<ILookup<Guid, ProjectBoardCardDependency>> LoadDependenciesAsync(
    BoardCardEntity[] cards,
    CancellationToken cancellationToken)
  {
    var cardIds = cards.Select(card => card.Id).ToArray();
    var cardsById = cards.ToDictionary(card => card.Id);
    var dependencies = await BoardCardDependencies
      .AsNoTracking()
      .Where(dependency => cardIds.Contains(dependency.CardId))
      .ToArrayAsync(cancellationToken);

    return dependencies
      .Where(dependency => cardsById.ContainsKey(dependency.DependsOnCardId))
      .ToLookup(
        dependency => dependency.CardId,
        dependency =>
        {
          var target = cardsById[dependency.DependsOnCardId];
          return new ProjectBoardCardDependency(target.Id, target.Title, target.IsCompleted);
        });
  }

  private Task<Dictionary<Guid, ProjectBoardCardUser>> LoadUsersAsync(
    Guid[] userIds,
    CancellationToken cancellationToken) =>
    AuthUsers
      .AsNoTracking()
      .Where(user => userIds.Contains(user.Id))
      .Select(user => new ProjectBoardCardUser(user.Id, user.Name, user.Email))
      .ToDictionaryAsync(user => user.UserId, cancellationToken);

  private static ProjectBoardCardSubtask ToSubtask(
    BoardCardSubtaskEntity subtask,
    Dictionary<Guid, ProjectBoardCardUser> users) =>
    new(
      subtask.Id,
      subtask.CardId,
      subtask.Title,
      subtask.IsCompleted,
      subtask.CompletedAt,
      subtask.CompletedByUserId is { } userId ? users.GetValueOrDefault(userId) : null,
      subtask.Position,
      subtask.CreatedAt,
      subtask.UpdatedAt);

  private static ProjectBoardList ToList(
    BoardListEntity entity,
    IReadOnlyCollection<ProjectBoardCard> cards) =>
    new(
      entity.Id,
      entity.BoardId,
      entity.ProjectId,
      entity.WorkspaceId,
      entity.Title,
      entity.Position,
      entity.TaskStatus,
      entity.CreatedAt,
      entity.UpdatedAt,
      cards);

  private static ProjectBoardCard ToCard(
    BoardCardEntity entity,
    IReadOnlyCollection<ProjectBoardCardAssignee> assignees,
    IReadOnlyCollection<ProjectBoardCardComment> comments,
    IReadOnlyCollection<ProjectBoardCardSubtask> subtasks,
    IReadOnlyCollection<ProjectBoardCardDependency> dependencies,
    ProjectBoardCardUser? completedBy) =>
    new(
      entity.Id,
      entity.BoardId,
      entity.ListId,
      entity.ProjectId,
      entity.WorkspaceId,
      entity.Title,
      entity.Description,
      entity.Priority,
      entity.DueDate,
      entity.EstimatedMinutes,
      entity.Labels,
      entity.IsCompleted,
      entity.CompletedAt,
      completedBy,
      entity.Position,
      entity.CreatedAt,
      entity.UpdatedAt,
      assignees,
      comments,
      subtasks,
      dependencies);

  private sealed record CardDetails(
    ILookup<Guid, ProjectBoardCardAssignee> Assignees,
    ILookup<Guid, ProjectBoardCardComment> Comments,
    ILookup<Guid, ProjectBoardCardSubtask> Subtasks,
    ILookup<Guid, ProjectBoardCardDependency> Dependencies,
    Dictionary<Guid, ProjectBoardCardUser> Users)
  {
    public ProjectBoardCard ToCard(BoardCardEntity card) =>
      PostgresBoardStore.ToCard(
        card,
        [.. Assignees[card.Id]],
        [.. Comments[card.Id]],
        [.. Subtasks[card.Id]],
        [.. Dependencies[card.Id]],
        card.CompletedByUserId is { } userId ? Users.GetValueOrDefault(userId) : null);
  }
}
