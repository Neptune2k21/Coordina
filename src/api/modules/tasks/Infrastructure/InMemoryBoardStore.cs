using System.Collections.Concurrent;
using Coordina.Api.Modules.Tasks.Application;
using Coordina.Api.Modules.Tasks.Domain;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class InMemoryBoardStore : IBoardStore
{
  private readonly ConcurrentDictionary<Guid, BoardRecord> _boards = new();
  private readonly ConcurrentDictionary<Guid, ListRecord> _lists = new();
  private readonly ConcurrentDictionary<Guid, CardRecord> _cards = new();

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

  public Task<ProjectBoard> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    string name,
    BoardTemplate boardTemplate,
    IReadOnlyCollection<BoardListSeed> listSeeds,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var board = new BoardRecord(
      Guid.NewGuid(),
      projectId,
      workspaceId,
      name,
      boardTemplate,
      createdAt,
      createdAt);
    _boards[board.Id] = board;

    var position = 0;
    foreach (var listSeed in listSeeds)
    {
      var list = new ListRecord(
        Guid.NewGuid(),
        board.Id,
        projectId,
        workspaceId,
        listSeed.Title,
        position++,
        createdAt,
        createdAt);
      _lists[list.Id] = list;

      var cardPosition = 0;
      foreach (var cardSeed in listSeed.Cards)
      {
        var card = new CardRecord(
          Guid.NewGuid(),
          board.Id,
          list.Id,
          projectId,
          workspaceId,
          cardSeed.Title,
          cardSeed.Description,
          cardSeed.Priority,
          null,
          cardSeed.Labels.ToArray(),
          cardPosition++,
          createdAt,
          createdAt,
          []);
        _cards[card.Id] = card;
      }
    }

    return Task.FromResult(ToBoard(board));
  }

  public Task<ProjectBoard?> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    string title,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var position = _lists.Values
      .Where(list => list.BoardId == boardId)
      .Select(list => (int?)list.Position)
      .Max() + 1 ?? 0;
    var list = new ListRecord(
      Guid.NewGuid(),
      boardId,
      projectId,
      workspaceId,
      title,
      position,
      createdAt,
      createdAt);
    _lists[list.Id] = list;
    _boards[boardId] = board with { UpdatedAt = createdAt };

    return Task.FromResult<ProjectBoard?>(ToBoard(_boards[boardId]));
  }

  public Task<ProjectBoard?> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    string title,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !_lists.TryGetValue(listId, out var list)
      || list.BoardId != boardId)
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    _lists[listId] = list with { Title = title, UpdatedAt = updatedAt };
    _boards[boardId] = board with { UpdatedAt = updatedAt };

    return Task.FromResult<ProjectBoard?>(ToBoard(_boards[boardId]));
  }

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
      mutation.Labels.ToArray(),
      position,
      createdAt,
      createdAt,
      mutation.AssigneeIds.ToArray());
    _cards[card.Id] = card;
    _boards[boardId] = board with { UpdatedAt = createdAt };

    return Task.FromResult<ProjectBoard?>(ToBoard(_boards[boardId]));
  }

  public Task<ProjectBoard?> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    BoardCardMutation mutation,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !_cards.TryGetValue(cardId, out var card)
      || card.BoardId != boardId)
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    _cards[cardId] = card with
    {
      Title = mutation.Title,
      Description = mutation.Description,
      Priority = mutation.Priority,
      DueDate = mutation.DueDate,
      Labels = mutation.Labels.ToArray(),
      UpdatedAt = updatedAt,
      AssigneeIds = mutation.AssigneeIds.ToArray()
    };
    _boards[boardId] = board with { UpdatedAt = updatedAt };

    return Task.FromResult<ProjectBoard?>(ToBoard(_boards[boardId]));
  }

  public Task<ProjectBoard?> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetList(workspaceId, projectId, boardId, listId, out _)
      || !_cards.TryGetValue(cardId, out var card)
      || card.BoardId != boardId)
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var position = _cards.Values
      .Where(candidate => candidate.BoardId == boardId
        && candidate.ListId == listId)
      .Select(candidate => (int?)candidate.Position)
      .Max() + 1 ?? 0;

    _cards[cardId] = card with
    {
      ListId = listId,
      Position = position,
      UpdatedAt = updatedAt
    };
    _boards[boardId] = board with { UpdatedAt = updatedAt };

    return Task.FromResult<ProjectBoard?>(ToBoard(_boards[boardId]));
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

    return Task.FromResult(_cards.TryRemove(cardId, out _));
  }

  private bool TryGetBoard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    out BoardRecord board)
  {
    return _boards.TryGetValue(boardId, out board!)
      && board.WorkspaceId == workspaceId
      && board.ProjectId == projectId;
  }

  private bool TryGetList(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    out ListRecord list)
  {
    return _lists.TryGetValue(listId, out list!)
      && list.WorkspaceId == workspaceId
      && list.ProjectId == projectId
      && list.BoardId == boardId;
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
      list.CreatedAt,
      list.UpdatedAt,
      _cards.Values
        .Where(card => card.ListId == list.Id)
        .OrderBy(card => card.Position)
        .Select(ToCard)
        .ToArray());
  }

  private static ProjectBoardCard ToCard(CardRecord card)
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
      card.Labels,
      card.Position,
      card.CreatedAt,
      card.UpdatedAt,
      card.AssigneeIds
        .Select(userId => new ProjectBoardCardAssignee(userId, null, null))
        .ToArray());
  }

  private sealed record BoardRecord(
    Guid Id,
    Guid ProjectId,
    Guid WorkspaceId,
    string Name,
    BoardTemplate Template,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

  private sealed record ListRecord(
    Guid Id,
    Guid BoardId,
    Guid ProjectId,
    Guid WorkspaceId,
    string Title,
    int Position,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

  private sealed record CardRecord(
    Guid Id,
    Guid BoardId,
    Guid ListId,
    Guid ProjectId,
    Guid WorkspaceId,
    string Title,
    string? Description,
    BoardCardPriority? Priority,
    DateOnly? DueDate,
    IReadOnlyCollection<string> Labels,
    int Position,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<Guid> AssigneeIds);
}
