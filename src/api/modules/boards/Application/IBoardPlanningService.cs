using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardPlanningService
{
  /// <summary>The user's saved preferences for the board, or the defaults.</summary>
  Task<Result<BoardPlanningPreferencesResponse>> GetPreferencesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken);

  /// <summary>Replaces the user's preferences; fields left out take their default.</summary>
  Task<Result<BoardPlanningPreferencesResponse>> SavePreferencesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    BoardPlanningRequest request,
    Guid userId,
    CancellationToken cancellationToken);
}
