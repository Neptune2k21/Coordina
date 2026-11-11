using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Building the domain model from the stored records.
public sealed partial class InMemoryBoardStore
{
  public Task<ProjectBoard?> FindDefaultForProjectAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var board = _boards.Values
      .Where(board => board.WorkspaceId == workspaceId
        && board.ProjectId == projectId)
      .OrderBy(board => board.CreatedAt)
      .FirstOrDefault();

    return Task.FromResult(board is null ? null : ToBoard(board));
  }

  public Task<ProjectBoard?> FindInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_boards.TryGetValue(boardId, out var board)
      || board.WorkspaceId != workspaceId
      || board.ProjectId != projectId)
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    return Task.FromResult<ProjectBoard?>(ToBoard(board));
  }

  private ProjectBoard ToBoard(BoardRecord board)
  {
    return new ProjectBoard(
      board.Id,
      board.ProjectId,
      board.WorkspaceId,
      board.Name,
      board.Template,
      board.CreatedAt,
      board.UpdatedAt,
      _lists.Values
        .Where(list => list.BoardId == board.Id)
        .OrderBy(list => list.Position)
        .Select(ToList)
        .ToArray());
  }

  private ProjectBoardList ToList(ListRecord list)
  {
    return new ProjectBoardList(
      list.Id,
      list.BoardId,
      list.ProjectId,
      list.WorkspaceId,
      list.Title,
      list.Position,
      list.TaskStatus,
      list.CreatedAt,
      list.UpdatedAt,
      _cards.Values
        .Where(card => card.ListId == list.Id)
        .OrderBy(card => card.Position)
        .Select(ToCard)
        .ToArray());
  }

  private ProjectBoardCard ToCard(CardRecord card)
  {
    return new ProjectBoardCard(
      card.Id,
      card.BoardId,
      card.ListId,
      card.ProjectId,
      card.WorkspaceId,
      card.Title,
      card.Description,
      card.Priority,
      card.DueDate,
      card.EstimatedMinutes,
      card.Labels,
      card.IsCompleted,
      card.CompletedAt,
      card.CompletedByUserId is null
        ? null
        : new ProjectBoardCardUser(card.CompletedByUserId.Value, null, null),
      card.Position,
      card.CreatedAt,
      card.UpdatedAt,
      card.AssigneeIds
        .Select(userId => new ProjectBoardCardAssignee(userId, null, null))
        .ToArray(),
      CommentsOf(card.Id),
      SubtasksOf(card.Id),
      DependenciesOf(card.Id));
  }

  private ProjectBoardCardComment[] CommentsOf(Guid cardId) =>
    _comments.Values
      .Where(comment => comment.CardId == cardId)
      .OrderBy(comment => comment.CreatedAt)
      .Select(comment => new ProjectBoardCardComment(
        comment.Id,
        comment.CardId,
        new ProjectBoardCardUser(comment.UserId, null, null),
        comment.Body,
        comment.CreatedAt,
        comment.EditedAt))
      .ToArray();

  private ProjectBoardCardSubtask[] SubtasksOf(Guid cardId) =>
    _subtasks.Values
      .Where(subtask => subtask.CardId == cardId)
      .OrderBy(subtask => subtask.Position)
      .Select(subtask => new ProjectBoardCardSubtask(
        subtask.Id,
        subtask.CardId,
        subtask.Title,
        subtask.IsCompleted,
        subtask.CompletedAt,
        subtask.CompletedByUserId is null
          ? null
          : new ProjectBoardCardUser(subtask.CompletedByUserId.Value, null, null),
        subtask.Position,
        subtask.CreatedAt,
        subtask.UpdatedAt))
      .ToArray();

  // A dependency on a card that no longer exists is not shown.
  private ProjectBoardCardDependency[] DependenciesOf(Guid cardId) =>
    _dependencies.Values
      .Where(dependency => dependency.CardId == cardId)
      .Select(dependency => _cards.TryGetValue(dependency.DependsOnCardId, out var target)
        ? new ProjectBoardCardDependency(target.Id, target.Title, target.IsCompleted)
        : null)
      .OfType<ProjectBoardCardDependency>()
      .ToArray();
}
