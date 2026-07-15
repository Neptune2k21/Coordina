using Coordina.Api.Modules.Projects.Domain;
using Coordina.Api.Modules.Workspaces.Application;

namespace Coordina.Api.Modules.Projects.Application;

public sealed class ProjectAccessGuard(
  IProjectStore projects,
  IWorkspaceStore workspaces) : IProjectAccessGuard
{
  public async Task<ProjectAccess?> FindAccessAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await workspaces.FindUserRoleAsync(
      workspaceId,
      userId,
      cancellationToken);

    if (role is null)
    {
      return null;
    }

    var project = await projects.FindInWorkspaceAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return project is null ? null : new ProjectAccess(role.Value, project);
  }

  public async Task<ProjectAccessCheck> FindWritableAccessAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access is null)
    {
      return new ProjectAccessCheck(ProjectAccessStatus.NotFound);
    }

    if (access.Project.Status == ProjectStatus.Completed)
    {
      return new ProjectAccessCheck(ProjectAccessStatus.ProjectReadOnly);
    }

    return new ProjectAccessCheck(ProjectAccessStatus.Granted, access);
  }
}
