using Coordina.Api.Modules.Projects.Domain;
using Coordina.Api.Modules.Workspaces.Domain;

namespace Coordina.Api.Modules.Projects.Application;

public sealed record ProjectAccess(WorkspaceRole Role, Project Project)
{
  public bool CanManageDestructiveActions(Guid userId) =>
    Role == WorkspaceRole.Owner || Project.ProjectOwnerId == userId;
}

public enum ProjectAccessStatus
{
  Granted,
  NotFound,
  ProjectReadOnly
}

public sealed record ProjectAccessCheck(
  ProjectAccessStatus Status,
  ProjectAccess? Access = null)
{
  public const string ReadOnlyMessage = "Completed projects are read-only.";
}

public interface IProjectAccessGuard
{
  Task<ProjectAccess?> FindAccessAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<ProjectAccessCheck> FindWritableAccessAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);
}
