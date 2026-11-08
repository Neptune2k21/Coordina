using System.Collections.Concurrent;
using Coordina.Api.Modules.Boards.Application;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class InMemoryBoardPlanningPreferenceStore : IBoardPlanningPreferenceStore
{
  private readonly ConcurrentDictionary<(Guid BoardId, Guid UserId), BoardPlanningPreferences> _preferences = new();

  public Task<BoardPlanningPreferences?> FindAsync(
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return Task.FromResult(_preferences.GetValueOrDefault((boardId, userId)));
  }

  public Task SaveAsync(
    Guid boardId,
    BoardPlanningPreferences preferences,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(preferences);
    cancellationToken.ThrowIfCancellationRequested();

    _preferences[(boardId, preferences.UserId)] = preferences;
    return Task.CompletedTask;
  }
}
