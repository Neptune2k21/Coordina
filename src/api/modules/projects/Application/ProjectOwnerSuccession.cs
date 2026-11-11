using Coordina.Api.Modules.Workspaces.Application;

namespace Coordina.Api.Modules.Projects.Application;

/// <summary>
/// Hands the projects of a member who leaves to the workspace owner the
/// departure names, so no project is left managed by someone outside it.
/// </summary>
public sealed class ProjectOwnerSuccession(
  IProjectStore projects,
  TimeProvider clock) : IWorkspaceDepartureHandler
{
  public Task HandleAsync(WorkspaceDeparture departure, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(departure);

    return projects.ReassignOwnerAsync(
      departure.WorkspaceId,
      departure.UserId,
      departure.SuccessorOwnerId,
      clock.GetUtcNow(),
      cancellationToken);
  }
}
