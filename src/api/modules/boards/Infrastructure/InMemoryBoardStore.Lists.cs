using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Creating, changing and deleting lists, and the positions they share.
public sealed partial class InMemoryBoardStore
{
  public Task<ProjectBoard?> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    string title,
    BoardTaskStatus taskStatus,
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
      taskStatus,
      createdAt,
      createdAt);
    _lists[list.Id] = list;
    return TouchedAsync(board, createdAt);
  }

  public Task<ProjectBoard?> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardListUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(update);
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetList(workspaceId, projectId, boardId, listId, out var list))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    _lists[listId] = list with
    {
      Title = update.Title,
      TaskStatus = update.TaskStatus,
      UpdatedAt = updatedAt
    };

    var order = OrderedLists(boardId).Where(other => other.Id != listId).ToList();
    order.Insert(Math.Clamp(update.Position, 0, order.Count), _lists[listId]);
    Renumber(order);

    if (update.CardCompletion is { } completion)
    {
      ApplyCompletion(listId, completion, updatedAt);
    }

    return TouchedAsync(board, updatedAt);
  }

  public Task<ProjectBoard?> DeleteListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardTransfer? transfer,
    DateTimeOffset deletedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetList(workspaceId, projectId, boardId, listId, out _)
      || (transfer is not null
        && !TryGetList(workspaceId, projectId, boardId, transfer.TargetListId, out _)))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var cards = _cards.Values
      .Where(card => card.ListId == listId)
      .OrderBy(card => card.Position)
      .ToArray();

    if (cards.Length > 0 && transfer is null)
    {
      throw new InvalidOperationException("A list with cards needs a target list for them.");
    }

    if (transfer is not null)
    {
      ApplyCompletion(listId, transfer.Completion, deletedAt);
      var position = NextCardPosition(boardId, transfer.TargetListId);

      foreach (var card in cards)
      {
        _cards[card.Id] = _cards[card.Id] with
        {
          ListId = transfer.TargetListId,
          Position = position++,
          UpdatedAt = deletedAt
        };
      }
    }

    _lists.TryRemove(listId, out _);
    Renumber(OrderedLists(boardId).ToList());
    return TouchedAsync(board, deletedAt);
  }

  private IEnumerable<ListRecord> OrderedLists(Guid boardId) =>
    _lists.Values
      .Where(list => list.BoardId == boardId)
      .OrderBy(list => list.Position)
      .ThenBy(list => list.CreatedAt);

  private void Renumber(List<ListRecord> order)
  {
    for (var position = 0; position < order.Count; position++)
    {
      _lists[order[position].Id] = order[position] with { Position = position };
    }
  }

  private void ApplyCompletion(Guid listId, BoardCardCompletion completion, DateTimeOffset updatedAt)
  {
    foreach (var card in _cards.Values.Where(card => card.ListId == listId
      && card.IsCompleted != completion.IsCompleted))
    {
      _cards[card.Id] = card with
      {
        IsCompleted = completion.IsCompleted,
        CompletedAt = completion.CompletedAt,
        CompletedByUserId = completion.CompletedByUserId,
        UpdatedAt = updatedAt
      };
    }
  }
}
