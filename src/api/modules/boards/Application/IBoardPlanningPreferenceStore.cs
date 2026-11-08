namespace Coordina.Api.Modules.Boards.Application;

/// <summary>Each user's saved planning preferences for a board.</summary>
public interface IBoardPlanningPreferenceStore
{
  Task<BoardPlanningPreferences?> FindAsync(
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken);

  Task SaveAsync(
    Guid boardId,
    BoardPlanningPreferences preferences,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);
}
