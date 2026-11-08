using Coordina.Api.Core.Persistence;
using Coordina.Api.Modules.Boards.Application;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class PostgresBoardPlanningPreferenceStore(CoordinaDbContext dbContext)
  : IBoardPlanningPreferenceStore
{
  public async Task<BoardPlanningPreferences?> FindAsync(
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var saved = await dbContext.Set<BoardPlanningPreferenceEntity>()
      .AsNoTracking()
      .SingleOrDefaultAsync(
        preference => preference.BoardId == boardId && preference.UserId == userId,
        cancellationToken);

    return saved is null
      ? null
      : new BoardPlanningPreferences(
        saved.UserId,
        saved.AvailableMinutes,
        saved.Context,
        saved.PreferAssigned,
        saved.IncludeUnassigned);
  }

  // An upsert, so two saves of the same user's preferences cannot both insert.
  public async Task SaveAsync(
    Guid boardId,
    BoardPlanningPreferences preferences,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(preferences);

    await dbContext.Database.ExecuteSqlInterpolatedAsync(
      $"""
      INSERT INTO board_planning_preferences
        (board_id, user_id, available_minutes, context, prefer_assigned, include_unassigned, updated_at)
      VALUES
        ({boardId}, {preferences.UserId}, {preferences.AvailableMinutes}, {preferences.Context},
         {preferences.PreferAssigned}, {preferences.IncludeUnassigned}, {updatedAt})
      ON CONFLICT (board_id, user_id) DO UPDATE SET
        available_minutes = EXCLUDED.available_minutes,
        context = EXCLUDED.context,
        prefer_assigned = EXCLUDED.prefer_assigned,
        include_unassigned = EXCLUDED.include_unassigned,
        updated_at = EXCLUDED.updated_at
      """,
      cancellationToken);
  }
}
