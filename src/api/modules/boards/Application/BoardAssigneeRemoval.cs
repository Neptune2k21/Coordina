using Coordina.Api.Modules.Workspaces.Application;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>
/// Unassigns a member who leaves the workspace from its cards, so no card
/// waits on someone who can no longer open the board.
/// </summary>
public sealed class BoardAssigneeRemoval(IBoardStore boards) : IWorkspaceDepartureHandler
{
  public Task HandleAsync(WorkspaceDeparture departure, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(departure);

    return boards.UnassignFromWorkspaceAsync(departure.WorkspaceId, departure.UserId, cancellationToken);
  }
}
