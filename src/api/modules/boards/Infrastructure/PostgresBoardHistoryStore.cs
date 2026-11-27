using System.Linq.Expressions;
using Coordina.Api.Core.Persistence;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class PostgresBoardHistoryStore(CoordinaDbContext dbContext) : IBoardHistoryStore
{
  private static readonly Expression<Func<BoardCardEventEntity, BoardCardEvent>> ToEvent =
    cardEvent => new BoardCardEvent(
      cardEvent.Id,
      cardEvent.CardId,
      cardEvent.Kind,
      cardEvent.FromValue,
      cardEvent.ToValue,
      cardEvent.OccurredAt);

  public async Task<IReadOnlyList<BoardCardEvent>> ListStatusEventsAsync(
    Guid boardId,
    CancellationToken cancellationToken) =>
    await Events(boardId)
      .Where(cardEvent => cardEvent.Kind == BoardCardEventKind.Created
        || cardEvent.Kind == BoardCardEventKind.StatusChanged)
      .OrderBy(cardEvent => cardEvent.Id)
      .Select(ToEvent)
      .ToListAsync(cancellationToken);

  public async Task<IReadOnlyList<BoardCardEvent>> ListEventsAsync(
    Guid boardId,
    long afterId,
    int limit,
    CancellationToken cancellationToken) =>
    await Events(boardId)
      .Where(cardEvent => cardEvent.Id > afterId)
      .OrderBy(cardEvent => cardEvent.Id)
      .Take(limit)
      .Select(ToEvent)
      .ToListAsync(cancellationToken);

  public async Task<IReadOnlyList<ForecastSnapshot>> ListSnapshotsAsync(
    Guid boardId,
    CancellationToken cancellationToken) =>
    await dbContext.Set<BoardForecastSnapshotEntity>()
      .AsNoTracking()
      .Where(snapshot => snapshot.BoardId == boardId)
      .OrderBy(snapshot => snapshot.TakenOn)
      .Select(snapshot => new ForecastSnapshot(
        snapshot.MilestoneId,
        snapshot.TakenOn,
        snapshot.TargetDate,
        snapshot.ForecastDate,
        snapshot.CommitDate,
        snapshot.OnTime,
        snapshot.OpenCardCount))
      .ToListAsync(cancellationToken);

  // Two servers may take the same day's snapshots: the first one wins.
  public async Task SaveSnapshotsAsync(
    Guid boardId,
    IReadOnlyCollection<ForecastSnapshot> snapshots,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(snapshots);

    foreach (var snapshot in snapshots)
    {
      await dbContext.Database.ExecuteSqlInterpolatedAsync(
        $"""
        INSERT INTO board_forecast_snapshots
          (board_id, milestone_id, taken_on, target_date, forecast_date, commit_date, on_time, open_card_count)
        VALUES
          ({boardId}, {snapshot.MilestoneId}, {snapshot.TakenOn}, {snapshot.TargetDate},
           {snapshot.ForecastDate}, {snapshot.CommitDate}, {snapshot.OnTime}, {snapshot.OpenCardCount})
        ON CONFLICT (milestone_id, taken_on) DO NOTHING
        """,
        cancellationToken);
    }
  }

  public async Task<IReadOnlyList<BoardKey>> ListBoardsWithoutSnapshotAsync(
    DateOnly day,
    Guid afterBoardId,
    int limit,
    CancellationToken cancellationToken)
  {
    var snapshots = dbContext.Set<BoardForecastSnapshotEntity>();

    return await dbContext.Set<BoardCardEntity>()
      .AsNoTracking()
      .Where(card => card.MilestoneDate != null
        && !card.IsCompleted
        && card.BoardId.CompareTo(afterBoardId) > 0
        && !snapshots.Any(snapshot => snapshot.MilestoneId == card.Id && snapshot.TakenOn == day))
      .Select(card => new { card.WorkspaceId, card.ProjectId, card.BoardId })
      .Distinct()
      .OrderBy(board => board.BoardId)
      .Take(limit)
      .Select(board => new BoardKey(board.WorkspaceId, board.ProjectId, board.BoardId))
      .ToListAsync(cancellationToken);
  }

  private IQueryable<BoardCardEventEntity> Events(Guid boardId) =>
    dbContext.Set<BoardCardEventEntity>()
      .AsNoTracking()
      .Where(cardEvent => cardEvent.BoardId == boardId);
}
