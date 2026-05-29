using Coordina.Api.Modules.Projects.Contracts;

namespace Coordina.Api.Modules.Projects.Application;

public interface IProjectService
{
  Task<ProjectResult<ProjectResponse>> CreateAsync(
    Guid workspaceId,
    CreateProjectRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<ProjectResult<IReadOnlyCollection<ProjectResponse>>> ListAsync(
    Guid workspaceId,
    bool includeArchived,
    bool includeCompleted,
    Guid userId,
    CancellationToken cancellationToken);

  Task<ProjectResult<ProjectResponse>> GetAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<ProjectResult<object>> DeleteAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<ProjectResult<ProjectResponse>> UpdateAsync(
    Guid workspaceId,
    Guid projectId,
    UpdateProjectRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<ProjectResult<object>> PermanentlyDeleteAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);
}
