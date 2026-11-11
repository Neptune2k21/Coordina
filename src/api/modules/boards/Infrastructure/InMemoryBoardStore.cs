using System.Collections.Concurrent;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

/// <summary>
/// The board store the API tests run against. Like PostgresBoardStore it is
/// split by area into partial files; this one holds the board itself and the
/// lookups the areas share.
/// </summary>
public sealed partial class InMemoryBoardStore : IBoardStore
{
  private readonly ConcurrentDictionary<Guid, BoardRecord> _boards = new();

  private readonly ConcurrentDictionary<Guid, ListRecord> _lists = new();

  private readonly ConcurrentDictionary<Guid, CardRecord> _cards = new();

  private readonly ConcurrentDictionary<Guid, CommentRecord> _comments = new();

  private readonly ConcurrentDictionary<Guid, SubtaskRecord> _subtasks = new();

  private readonly ConcurrentDictionary<Guid, DependencyRecord> _dependencies = new();

  // Only the fast tests use this store, and they do not race on a board.
  // Concurrency is covered against PostgreSQL in tests/api-postgres.
  public Task<T> RunExclusiveAsync<T>(
    Guid boardId,
    Func<CancellationToken, Task<T>> action,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);

    return action(cancellationToken);
  }

  public Task<ProjectBoard?> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    string name,
    BoardTemplate boardTemplate,
    IReadOnlyCollection<BoardListSeed> listSeeds,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (_boards.Values.Any(existing => existing.ProjectId == projectId))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

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
      AddSeededList(board, listSeed, position++);
    }

    return Task.FromResult<ProjectBoard?>(ToBoard(board));
  }

  // A list seeded from a template, with its starter cards. Cards in a DONE
  // list start completed, like their subtasks.
  private void AddSeededList(BoardRecord board, BoardListSeed seed, int position)
  {
    var list = new ListRecord(
      Guid.NewGuid(),
      board.Id,
      board.ProjectId,
      board.WorkspaceId,
      seed.Title,
      position,
      seed.TaskStatus,
      board.CreatedAt,
      board.CreatedAt);
    _lists[list.Id] = list;

    var isDone = seed.TaskStatus == BoardTaskStatus.Done;
    var cardPosition = 0;

    foreach (var cardSeed in seed.Cards)
    {
      AddSeededCard(list, cardSeed, cardPosition++, isDone);
    }
  }

  private void AddSeededCard(ListRecord list, BoardCardSeed seed, int position, bool isDone)
  {
    var createdAt = list.CreatedAt;
    var card = new CardRecord(
      seed.Id,
      list.BoardId,
      list.Id,
      list.ProjectId,
      list.WorkspaceId,
      seed.Title,
      seed.Description,
      seed.Priority,
      seed.DueDate,
      seed.Size,
      seed.Labels.ToArray(),
      isDone,
      isDone ? createdAt : null,
      null,
      position,
      createdAt,
      createdAt,
      []);
    _cards[card.Id] = card;

    var subtaskPosition = 0;
    foreach (var title in seed.Subtasks)
    {
      var subtask = new SubtaskRecord(
        Guid.NewGuid(),
        card.Id,
        title,
        isDone,
        isDone ? createdAt : null,
        null,
        subtaskPosition++,
        createdAt,
        createdAt);
      _subtasks[subtask.Id] = subtask;
    }

    foreach (var dependsOnCardId in seed.DependsOnCardIds)
    {
      var dependency = new DependencyRecord(Guid.NewGuid(), card.Id, dependsOnCardId, createdAt);
      _dependencies[dependency.Id] = dependency;
    }
  }

  private int NextCardPosition(Guid boardId, Guid listId)
  {
    return _cards.Values
      .Where(candidate => candidate.BoardId == boardId
        && candidate.ListId == listId)
      .Select(candidate => (int?)candidate.Position)
      .Max() + 1 ?? 0;
  }

  private bool TryGetCardInList(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    out BoardRecord board,
    out CardRecord card)
  {
    card = null!;

    return TryGetBoard(workspaceId, projectId, boardId, out board)
      && TryGetList(workspaceId, projectId, boardId, listId, out _)
      && _cards.TryGetValue(cardId, out card!)
      && card.BoardId == boardId;
  }

  private bool TryGetSubtask(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid subtaskId,
    out BoardRecord board,
    out CardRecord card,
    out SubtaskRecord subtask)
  {
    card = null!;
    subtask = null!;

    return TryGetBoard(workspaceId, projectId, boardId, out board)
      && TryGetCard(workspaceId, projectId, boardId, cardId, out card)
      && _subtasks.TryGetValue(subtaskId, out subtask!)
      && subtask.CardId == cardId;
  }

  // Records that the board changed and returns it as the API sees it.
  private Task<ProjectBoard?> TouchedAsync(BoardRecord board, DateTimeOffset updatedAt)
  {
    _boards[board.Id] = board with { UpdatedAt = updatedAt };

    return Task.FromResult<ProjectBoard?>(ToBoard(_boards[board.Id]));
  }

  private Task<ProjectBoard?> TouchedWithCardAsync(BoardRecord board, CardRecord card, DateTimeOffset updatedAt)
  {
    _cards[card.Id] = card with { UpdatedAt = updatedAt };

    return TouchedAsync(board, updatedAt);
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

  private bool TryGetCard(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    out CardRecord card)
  {
    return _cards.TryGetValue(cardId, out card!)
      && card.WorkspaceId == workspaceId
      && card.ProjectId == projectId
      && card.BoardId == boardId;
  }
}
