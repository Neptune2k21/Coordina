using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Projects.Domain;
using Coordina.Api.Modules.Tasks.Contracts;
using Coordina.Api.Modules.Tasks.Domain;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Domain;
using TaskStatus = Coordina.Api.Modules.Tasks.Domain.TaskStatus;

namespace Coordina.Api.Modules.Tasks.Application;

public sealed class TaskService(
  ITaskStore tasks,
  IProjectStore projects,
  IWorkspaceStore workspaces) : ITaskService
{
  public async Task<TaskResult<TaskResponse>> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    CreateTaskRequest request,
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
      return new TaskResult<TaskResponse>(TaskResultStatus.NotFound);
    }

    if (access.Project.Status == ProjectStatus.Completed)
    {
      return new TaskResult<TaskResponse>(
        TaskResultStatus.Conflict,
        Message: "Completed projects are read-only.");
    }

    var priority = ParsePriority(request.Priority, out var priorityError);
    var errors = ValidateFields(
      request.Title,
      request.Description,
      priorityError);

    if (errors.Count > 0)
    {
      return new TaskResult<TaskResponse>(
        TaskResultStatus.ValidationError,
        Errors: errors);
    }

    var now = DateTimeOffset.UtcNow;
    var task = await tasks.CreateAsync(
      workspaceId,
      projectId,
      request.Title!.Trim(),
      NormalizeText(request.Description),
      priority,
      now,
      cancellationToken);

    return new TaskResult<TaskResponse>(
      TaskResultStatus.Success,
      ToResponse(task));
  }

  public async Task<TaskResult<IReadOnlyCollection<TaskResponse>>> ListAsync(
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
      return new TaskResult<IReadOnlyCollection<TaskResponse>>(
        TaskResultStatus.NotFound);
    }

    var scopedTasks = await tasks.ListForProjectAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return new TaskResult<IReadOnlyCollection<TaskResponse>>(
      TaskResultStatus.Success,
      scopedTasks.Select(ToResponse).ToArray());
  }

  public async Task<TaskResult<TaskResponse>> UpdateAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    UpdateTaskRequest request,
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
      return new TaskResult<TaskResponse>(TaskResultStatus.NotFound);
    }

    if (access.Project.Status == ProjectStatus.Completed)
    {
      return new TaskResult<TaskResponse>(
        TaskResultStatus.Conflict,
        Message: "Completed projects are read-only.");
    }

    var task = await tasks.FindInProjectAsync(
      workspaceId,
      projectId,
      taskId,
      cancellationToken);

    if (task is null)
    {
      return new TaskResult<TaskResponse>(TaskResultStatus.NotFound);
    }

    string? priorityError = null;
    var priority = request.Priority is null
      ? task.Priority
      : ParsePriority(request.Priority, out priorityError);
    var errors = ValidateFields(
      request.Title ?? task.Title,
      request.Description,
      request.Priority is null ? null : priorityError);

    if (errors.Count > 0)
    {
      return new TaskResult<TaskResponse>(
        TaskResultStatus.ValidationError,
        Errors: errors);
    }

    var update = new TaskUpdate(
      request.Title is null ? task.Title : request.Title.Trim(),
      request.Description is null
        ? task.Description
        : NormalizeText(request.Description),
      priority);

    var updated = await tasks.UpdateInProjectAsync(
      workspaceId,
      projectId,
      taskId,
      update,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return updated is null
      ? new TaskResult<TaskResponse>(TaskResultStatus.NotFound)
      : new TaskResult<TaskResponse>(
        TaskResultStatus.Success,
        ToResponse(updated));
  }

  public async Task<TaskResult<TaskResponse>> ChangeStatusAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    ChangeTaskStatusRequest request,
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
      return new TaskResult<TaskResponse>(TaskResultStatus.NotFound);
    }

    if (access.Project.Status == ProjectStatus.Completed)
    {
      return new TaskResult<TaskResponse>(
        TaskResultStatus.Conflict,
        Message: "Completed projects are read-only.");
    }

    var status = ParseStatus(request.Status, out var statusError);

    if (statusError is not null)
    {
      return new TaskResult<TaskResponse>(
        TaskResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.Status)] = [statusError]
        });
    }

    var current = await tasks.FindInProjectAsync(
      workspaceId,
      projectId,
      taskId,
      cancellationToken);

    if (current is null)
    {
      return new TaskResult<TaskResponse>(TaskResultStatus.NotFound);
    }

    var updated = await tasks.ChangeStatusInProjectAsync(
      workspaceId,
      projectId,
      taskId,
      status!.Value,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return updated is null
      ? new TaskResult<TaskResponse>(TaskResultStatus.NotFound)
      : new TaskResult<TaskResponse>(
        TaskResultStatus.Success,
        ToResponse(updated));
  }

  public async Task<TaskResult<object>> DeleteAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
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
      return new TaskResult<object>(TaskResultStatus.NotFound);
    }

    if (access.Project.Status == ProjectStatus.Completed)
    {
      return new TaskResult<object>(
        TaskResultStatus.Conflict,
        Message: "Completed projects are read-only.");
    }

    if (!CanDeleteTask(access.Role, access.Project, userId))
    {
      return new TaskResult<object>(
        TaskResultStatus.Forbidden,
        Message: "Only workspace owners or project owners can delete tasks.");
    }

    var deleted = await tasks.DeleteInProjectAsync(
      workspaceId,
      projectId,
      taskId,
      cancellationToken);

    return deleted
      ? new TaskResult<object>(TaskResultStatus.Success)
      : new TaskResult<object>(TaskResultStatus.NotFound);
  }

  private async Task<ProjectAccess?> FindAccessAsync(
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

  private static bool CanDeleteTask(
    WorkspaceRole role,
    Project project,
    Guid userId) =>
    role == WorkspaceRole.Owner || project.ProjectOwnerId == userId;

  private static TaskPriority? ParsePriority(
    string? priority,
    out string? error)
  {
    error = null;

    if (string.IsNullOrWhiteSpace(priority))
    {
      return null;
    }

    if (Enum.TryParse<TaskPriority>(priority, true, out var parsed))
    {
      return parsed;
    }

    error = "Task priority must be LOW, MEDIUM, or HIGH.";
    return null;
  }

  private static TaskStatus? ParseStatus(
    string? status,
    out string? error)
  {
    error = null;

    if (string.IsNullOrWhiteSpace(status))
    {
      error = "Task status is required.";
      return null;
    }

    var normalized = status.Replace("_", string.Empty, StringComparison.Ordinal);

    if (Enum.TryParse<TaskStatus>(normalized, true, out var parsed))
    {
      return parsed;
    }

    error = "Task status must be TODO, IN_PROGRESS, or DONE.";
    return null;
  }

  private static Dictionary<string, string[]> ValidateFields(
    string? title,
    string? description,
    string? priorityError)
  {
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(title))
    {
      errors["Title"] = ["Task title is required."];
    }
    else if (title.Trim().Length > 160)
    {
      errors["Title"] = ["Task title must be 160 characters or fewer."];
    }

    if (description?.Trim().Length > 1000)
    {
      errors["Description"] = [
        "Task description must be 1000 characters or fewer."
      ];
    }

    if (priorityError is not null)
    {
      errors["Priority"] = [priorityError];
    }

    return errors;
  }

  private static string? NormalizeText(string? value)
  {
    var normalized = value?.Trim();
    return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
  }

  private static string ToApiStatus(TaskStatus status) =>
    status == TaskStatus.InProgress
      ? "IN_PROGRESS"
      : status.ToString().ToUpperInvariant();

  private static TaskResponse ToResponse(ProjectTask task) =>
    new(
      task.Id,
      task.ProjectId,
      task.Title,
      task.Description,
      ToApiStatus(task.Status),
      task.Priority?.ToString().ToUpperInvariant(),
      task.CreatedAt,
      task.UpdatedAt);

  private sealed record ProjectAccess(WorkspaceRole Role, Project Project);
}
