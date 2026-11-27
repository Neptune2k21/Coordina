using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

internal static class BoardReading
{
  /// <summary>The board, when the user may read its project; not found otherwise.</summary>
  public static async Task<Result<ProjectBoard>> FindReadableAsync(
    this IBoardStore boards,
    IProjectAccessGuard projectAccess,
    BoardTarget target,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.AuthorizeReadAsync(
      target.WorkspaceId,
      target.ProjectId,
      target.UserId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    var board = await boards.FindInProjectAsync(
      target.WorkspaceId,
      target.ProjectId,
      target.BoardId,
      cancellationToken);

    return board is null ? Error.NotFound() : Result.Success(board);
  }
}

/// <summary>A board, and the user asking about it.</summary>
public sealed record BoardTarget(Guid WorkspaceId, Guid ProjectId, Guid BoardId, Guid UserId);
