using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>
/// What happened on boards, and what their forecasts said. Card events are
/// written by the database itself, whichever way a card changes.
/// </summary>
public interface IBoardHistoryStore
{
  /// <summary>Every change of the cards' task status, oldest first.</summary>
  Task<IReadOnlyList<BoardCardEvent>> ListStatusEventsAsync(
    Guid boardId,
    CancellationToken cancellationToken);

  /// <summary>The events after the given one, oldest first, at most <paramref name="limit"/> of them.</summary>
  Task<IReadOnlyList<BoardCardEvent>> ListEventsAsync(
    Guid boardId,
    long afterId,
    int limit,
    CancellationToken cancellationToken);

  Task<IReadOnlyList<ForecastSnapshot>> ListSnapshotsAsync(
    Guid boardId,
    CancellationToken cancellationToken);

  /// <summary>Saves the day's snapshots; one already saved that day is kept.</summary>
  Task SaveSnapshotsAsync(
    Guid boardId,
    IReadOnlyCollection<ForecastSnapshot> snapshots,
    CancellationToken cancellationToken);

  /// <summary>
  /// Boards with an open milestone that has no snapshot for that day, in id
  /// order after the given one.
  /// </summary>
  Task<IReadOnlyList<BoardKey>> ListBoardsWithoutSnapshotAsync(
    DateOnly day,
    Guid afterBoardId,
    int limit,
    CancellationToken cancellationToken);
}

public sealed record BoardKey(Guid WorkspaceId, Guid ProjectId, Guid BoardId);
