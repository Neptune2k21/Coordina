using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

public sealed class BoardPlanningService(
  IBoardStore boards,
  IBoardPlanningPreferenceStore preferences,
  IProjectAccessGuard projectAccess,
  TimeProvider clock) : IBoardPlanningService
{
  private static readonly BoardPlanningRequest Defaults = new();

  public async Task<Result<BoardPlanningPreferencesResponse>> GetPreferencesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var board = await FindReadableBoardAsync(workspaceId, projectId, boardId, userId, cancellationToken);

    if (!board.IsSuccess)
    {
      return board.Error;
    }

    var saved = await preferences.FindAsync(boardId, userId, cancellationToken)
      ?? BoardPlanningPreferences.Parse(Defaults, userId).Value;

    return Result.Success(ToResponse(saved));
  }

  public async Task<Result<BoardPlanningPreferencesResponse>> SavePreferencesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    BoardPlanningRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    // Preferences are personal, so reading the project is enough to keep them.
    var board = await FindReadableBoardAsync(workspaceId, projectId, boardId, userId, cancellationToken);

    if (!board.IsSuccess)
    {
      return board.Error;
    }

    var parsed = BoardPlanningPreferences.Parse(request, userId);

    if (!parsed.IsSuccess)
    {
      return parsed.Error;
    }

    await preferences.SaveAsync(boardId, parsed.Value, clock.GetUtcNow(), cancellationToken);

    return Result.Success(ToResponse(parsed.Value));
  }

  private async Task<Result> FindReadableBoardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.AuthorizeReadAsync(workspaceId, projectId, userId, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    return await boards.FindInProjectAsync(workspaceId, projectId, boardId, cancellationToken) is null
      ? Error.NotFound()
      : Result.Success();
  }

  private static BoardPlanningPreferencesResponse ToResponse(BoardPlanningPreferences saved) =>
    new(saved.AvailableMinutes, saved.Context, saved.PreferAssigned, saved.IncludeUnassigned);
}
