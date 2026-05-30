using System.Collections.Concurrent;
using Coordina.Api.Modules.Tasks.Application;
using Coordina.Api.Modules.Tasks.Domain;
using TaskStatus = Coordina.Api.Modules.Tasks.Domain.TaskStatus;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class InMemoryTaskStore : ITaskStore
{
  private readonly ConcurrentDictionary<Guid, ProjectTask> _tasks = new();

  public Task<ProjectTask> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    string title,
    string? description,
    TaskPriority? priority,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var task = new ProjectTask(
      Guid.NewGuid(),
      projectId,
      workspaceId,
      title,
      description,
      TaskStatus.Todo,
      priority,
      createdAt,
      createdAt);

    _tasks[task.Id] = task;

    return Task.FromResult(task);
  }

  public Task<IReadOnlyCollection<ProjectTask>> ListForProjectAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var tasks = _tasks.Values
      .Where(task => task.WorkspaceId == workspaceId
        && task.ProjectId == projectId)
      .OrderBy(task => task.Status)
      .ThenBy(task => task.CreatedAt)
      .ToArray();

    return Task.FromResult<IReadOnlyCollection<ProjectTask>>(tasks);
  }

  public Task<ProjectTask?> FindInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_tasks.TryGetValue(taskId, out var task)
      || task.WorkspaceId != workspaceId
      || task.ProjectId != projectId)
    {
      return Task.FromResult<ProjectTask?>(null);
    }

    return Task.FromResult<ProjectTask?>(task);
  }

  public Task<ProjectTask?> UpdateInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    TaskUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_tasks.TryGetValue(taskId, out var task)
      || task.WorkspaceId != workspaceId
      || task.ProjectId != projectId)
    {
      return Task.FromResult<ProjectTask?>(null);
    }

    var updated = task with
    {
      Title = update.Title,
      Description = update.Description,
      Priority = update.Priority,
      UpdatedAt = updatedAt
    };

    _tasks[taskId] = updated;

    return Task.FromResult<ProjectTask?>(updated);
  }

  public Task<ProjectTask?> ChangeStatusInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    TaskStatus status,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_tasks.TryGetValue(taskId, out var task)
      || task.WorkspaceId != workspaceId
      || task.ProjectId != projectId)
    {
      return Task.FromResult<ProjectTask?>(null);
    }

    var updated = task with
    {
      Status = status,
      UpdatedAt = updatedAt
    };

    _tasks[taskId] = updated;

    return Task.FromResult<ProjectTask?>(updated);
  }

  public Task<bool> DeleteInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_tasks.TryGetValue(taskId, out var task)
      || task.WorkspaceId != workspaceId
      || task.ProjectId != projectId)
    {
      return Task.FromResult(false);
    }

    return Task.FromResult(_tasks.TryRemove(taskId, out _));
  }
}
