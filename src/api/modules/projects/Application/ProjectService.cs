using Coordina.Api.Modules.Projects.Contracts;
using Coordina.Api.Modules.Projects.Domain;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Domain;

namespace Coordina.Api.Modules.Projects.Application;

public sealed class ProjectService(
  IProjectStore projects,
  IWorkspaceStore workspaces) : IProjectService
{
  public async Task<ProjectResult<ProjectResponse>> CreateAsync(
    Guid workspaceId,
    CreateProjectRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await FindRoleAsync(workspaceId, userId, cancellationToken);

    if (role is null)
    {
      return new ProjectResult<ProjectResponse>(ProjectResultStatus.NotFound);
    }

    var ownerId = request.ProjectOwnerId ?? userId;
    var ownerRole = await FindRoleAsync(workspaceId, ownerId, cancellationToken);

    if (ownerRole is null)
    {
      return new ProjectResult<ProjectResponse>(
        ProjectResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.ProjectOwnerId)] = ["Project owner must belong to the workspace."]
        });
    }

    var errors = ValidateCreate(request);

    if (errors.Count > 0)
    {
      return new ProjectResult<ProjectResponse>(
        ProjectResultStatus.ValidationError,
        Errors: errors);
    }

    var now = DateTimeOffset.UtcNow;
    var project = await projects.CreateAsync(
      workspaceId,
      request.Name.Trim(),
      NormalizeText(request.Description),
      NormalizeKey(request.Key),
      NormalizeText(request.Icon),
      NormalizeText(request.Color),
      ownerId,
      now,
      cancellationToken);

    return new ProjectResult<ProjectResponse>(
      ProjectResultStatus.Success,
      ToResponse(project));
  }

  public async Task<ProjectResult<IReadOnlyCollection<ProjectResponse>>> ListAsync(
    Guid workspaceId,
    bool includeArchived,
    bool includeCompleted,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await FindRoleAsync(workspaceId, userId, cancellationToken);

    if (role is null)
    {
      return new ProjectResult<IReadOnlyCollection<ProjectResponse>>(
        ProjectResultStatus.NotFound);
    }

    var scopedProjects = await projects.ListForWorkspaceAsync(
      workspaceId,
      includeArchived,
      includeCompleted,
      cancellationToken);

    return new ProjectResult<IReadOnlyCollection<ProjectResponse>>(
      ProjectResultStatus.Success,
      scopedProjects.Select(ToResponse).ToArray());
  }

  public async Task<ProjectResult<ProjectResponse>> GetAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await FindRoleAsync(workspaceId, userId, cancellationToken);

    if (role is null)
    {
      return new ProjectResult<ProjectResponse>(ProjectResultStatus.NotFound);
    }

    var project = await projects.FindInWorkspaceAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return project is null
      ? new ProjectResult<ProjectResponse>(ProjectResultStatus.NotFound)
      : new ProjectResult<ProjectResponse>(
        ProjectResultStatus.Success,
        ToResponse(project));
  }

  public async Task<ProjectResult<ProjectResponse>> UpdateAsync(
    Guid workspaceId,
    Guid projectId,
    UpdateProjectRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await FindRoleAsync(workspaceId, userId, cancellationToken);

    if (role is null)
    {
      return new ProjectResult<ProjectResponse>(ProjectResultStatus.NotFound);
    }

    var project = await projects.FindInWorkspaceAsync(
      workspaceId,
      projectId,
      cancellationToken);

    if (project is null)
    {
      return new ProjectResult<ProjectResponse>(ProjectResultStatus.NotFound);
    }

    if (project.Status == ProjectStatus.Completed)
    {
      return new ProjectResult<ProjectResponse>(
        ProjectResultStatus.Conflict,
        Message: "Completed projects are read-only.");
    }

    if (!CanManageProject(role.Value, project, userId))
    {
      return new ProjectResult<ProjectResponse>(
        ProjectResultStatus.Forbidden,
        Message: "Only workspace owners or project owners can update projects.");
    }

    var status = ParseStatus(request.Status, out var statusError)
      ?? project.Status;

    if (statusError is not null)
    {
      return new ProjectResult<ProjectResponse>(
        ProjectResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.Status)] = [statusError]
        });
    }

    var ownerId = request.ProjectOwnerId ?? project.ProjectOwnerId;
    var ownerRole = await FindRoleAsync(workspaceId, ownerId, cancellationToken);

    if (ownerRole is null)
    {
      return new ProjectResult<ProjectResponse>(
        ProjectResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.ProjectOwnerId)] = ["Project owner must belong to the workspace."]
        });
    }

    var update = new ProjectUpdate(
      NormalizeRequiredText(request.Name) ?? project.Name,
      request.Description is null
        ? project.Description
        : NormalizeText(request.Description),
      request.Key is null ? project.Key : NormalizeKey(request.Key),
      request.Icon is null ? project.Icon : NormalizeText(request.Icon),
      request.Color is null ? project.Color : NormalizeText(request.Color),
      ownerId,
      status);

    var errors = ValidateUpdate(update);

    if (errors.Count > 0)
    {
      return new ProjectResult<ProjectResponse>(
        ProjectResultStatus.ValidationError,
        Errors: errors);
    }

    var updatedProject = await projects.UpdateInWorkspaceAsync(
      workspaceId,
      projectId,
      update,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return updatedProject is null
      ? new ProjectResult<ProjectResponse>(ProjectResultStatus.NotFound)
      : new ProjectResult<ProjectResponse>(
        ProjectResultStatus.Success,
        ToResponse(updatedProject));
  }

  public async Task<ProjectResult<object>> DeleteAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await FindRoleAsync(workspaceId, userId, cancellationToken);

    if (role is null)
    {
      return new ProjectResult<object>(ProjectResultStatus.NotFound);
    }

    var project = await projects.FindInWorkspaceAsync(
      workspaceId,
      projectId,
      cancellationToken);

    if (project is null)
    {
      return new ProjectResult<object>(ProjectResultStatus.NotFound);
    }

    if (project.Status == ProjectStatus.Completed)
    {
      return new ProjectResult<object>(
        ProjectResultStatus.Conflict,
        Message: "Completed projects are read-only.");
    }

    if (!CanManageProject(role.Value, project, userId))
    {
      return new ProjectResult<object>(
        ProjectResultStatus.Forbidden,
        Message: "Only workspace owners or project owners can archive projects.");
    }

    var archived = await projects.ArchiveInWorkspaceAsync(
      workspaceId,
      projectId,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return archived
      ? new ProjectResult<object>(ProjectResultStatus.Success)
      : new ProjectResult<object>(ProjectResultStatus.NotFound);
  }

  public async Task<ProjectResult<object>> PermanentlyDeleteAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await FindRoleAsync(workspaceId, userId, cancellationToken);

    if (role is null)
    {
      return new ProjectResult<object>(ProjectResultStatus.NotFound);
    }

    if (role != WorkspaceRole.Owner)
    {
      return new ProjectResult<object>(
        ProjectResultStatus.Forbidden,
        Message: "Only workspace owners can permanently delete projects.");
    }

    var deleted = await projects.PermanentlyDeleteInWorkspaceAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return deleted
      ? new ProjectResult<object>(ProjectResultStatus.Success)
      : new ProjectResult<object>(ProjectResultStatus.NotFound);
  }

  private Task<WorkspaceRole?> FindRoleAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken) =>
    workspaces.FindUserRoleAsync(workspaceId, userId, cancellationToken);

  private static bool CanManageProject(
    WorkspaceRole role,
    Project project,
    Guid userId) =>
    role == WorkspaceRole.Owner || project.ProjectOwnerId == userId;

  private static ProjectStatus? ParseStatus(
    string? status,
    out string? error)
  {
    error = null;

    if (string.IsNullOrWhiteSpace(status))
    {
      return null;
    }

    if (Enum.TryParse<ProjectStatus>(status, true, out var parsed))
    {
      return parsed;
    }

    error = "Project status must be ACTIVE, ARCHIVED, or COMPLETED.";
    return null;
  }

  private static Dictionary<string, string[]> ValidateCreate(
    CreateProjectRequest request)
  {
    return ValidateFields(
      request.Name,
      request.Description,
      request.Key,
      request.Icon,
      request.Color);
  }

  private static Dictionary<string, string[]> ValidateUpdate(ProjectUpdate update)
  {
    return ValidateFields(
      update.Name,
      update.Description,
      update.Key,
      update.Icon,
      update.Color);
  }

  private static Dictionary<string, string[]> ValidateFields(
    string? name,
    string? description,
    string? key,
    string? icon,
    string? color)
  {
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(name))
    {
      errors["Name"] = ["Project name is required."];
    }
    else if (name.Trim().Length < 2)
    {
      errors["Name"] = ["Project name must be at least 2 characters."];
    }
    else if (name.Trim().Length > 100)
    {
      errors["Name"] = ["Project name must be 100 characters or fewer."];
    }

    if (description?.Trim().Length > 500)
    {
      errors["Description"] = [
        "Project description must be 500 characters or fewer."
      ];
    }

    if (key?.Trim().Length > 12)
    {
      errors["Key"] = ["Project key must be 12 characters or fewer."];
    }

    if (icon?.Trim().Length > 16)
    {
      errors["Icon"] = ["Project icon must be 16 characters or fewer."];
    }

    if (color?.Trim().Length > 32)
    {
      errors["Color"] = ["Project color must be 32 characters or fewer."];
    }

    return errors;
  }

  private static string? NormalizeRequiredText(string? value)
  {
    var normalized = value?.Trim();
    return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
  }

  private static string? NormalizeText(string? value)
  {
    var normalized = value?.Trim();
    return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
  }

  private static string? NormalizeKey(string? value)
  {
    var normalized = NormalizeText(value);
    return normalized?.ToUpperInvariant();
  }

  private static ProjectResponse ToResponse(Project project) =>
    new(
      project.Id,
      project.Name,
      project.Description,
      project.Key,
      project.Icon,
      project.Color,
      project.WorkspaceId,
      project.ProjectOwnerId,
      project.ProjectOwnerName,
      project.Status.ToString().ToUpperInvariant(),
      project.CreatedAt,
      project.UpdatedAt,
      project.ArchivedAt);
}
