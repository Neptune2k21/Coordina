namespace Coordina.Api.Modules.Workspaces.Application;

/// <summary>
/// Lets the modules that point at workspace members, which this module cannot
/// reference, let go of a member who leaves. Handlers run inside the
/// departure's transaction, so a failure keeps the member and their data
/// together.
/// </summary>
public interface IWorkspaceDepartureHandler
{
  Task HandleAsync(WorkspaceDeparture departure, CancellationToken cancellationToken);
}
