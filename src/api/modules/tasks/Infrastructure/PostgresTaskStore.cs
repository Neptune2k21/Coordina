using Coordina.Api.Infrastructure.Persistence;
using Coordina.Api.Modules.Tasks.Application;
using Coordina.Api.Modules.Tasks.Domain;
using Microsoft.EntityFrameworkCore;
using TaskStatus = Coordina.Api.Modules.Tasks.Domain.TaskStatus;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class PostgresTaskStore(CoordinaDbContext dbContext)
  : ITaskStore
{
  public async Task<ProjectTask> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    string title,
    string? description,
    TaskPriority? priority,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var entity = new TaskEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      ProjectId = projectId,
      Title = title,
      Description = description,
      Status = TaskStatus.Todo,
      Priority = priority,
      CreatedAt = createdAt,
      UpdatedAt = createdAt
    };

    dbContext.Tasks.Add(entity);
    await dbContext.SaveChangesAsync(cancellationToken);

    return ToTask(entity);
  }

  public async Task<IReadOnlyCollection<ProjectTask>> ListForProjectAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken)
  {
    return await dbContext.Tasks
      .AsNoTracking()
      .Where(task => task.WorkspaceId == workspaceId
        && task.ProjectId == projectId)
      .OrderBy(task => task.Status)
      .ThenBy(task => task.CreatedAt)
      .Select(task => new ProjectTask(
        task.Id,
        task.ProjectId,
        task.WorkspaceId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.CreatedAt,
        task.UpdatedAt))
      .ToArrayAsync(cancellationToken);
  }

  public async Task<ProjectTask?> FindInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    CancellationToken cancellationToken)
  {
    return await dbContext.Tasks
      .AsNoTracking()
      .Where(task => task.WorkspaceId == workspaceId
        && task.ProjectId == projectId
        && task.Id == taskId)
      .Select(task => new ProjectTask(
        task.Id,
        task.ProjectId,
        task.WorkspaceId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.CreatedAt,
        task.UpdatedAt))
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<ProjectTask?> UpdateInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    TaskUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var updated = await dbContext.Tasks
      .Where(task => task.WorkspaceId == workspaceId
        && task.ProjectId == projectId
        && task.Id == taskId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(task => task.Title, update.Title)
        .SetProperty(task => task.Description, update.Description)
        .SetProperty(task => task.Priority, update.Priority)
        .SetProperty(task => task.UpdatedAt, updatedAt),
        cancellationToken);

    return updated == 0
      ? null
      : await FindInProjectAsync(
        workspaceId,
        projectId,
        taskId,
        cancellationToken);
  }

  public async Task<ProjectTask?> ChangeStatusInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    TaskStatus status,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var updated = await dbContext.Tasks
      .Where(task => task.WorkspaceId == workspaceId
        && task.ProjectId == projectId
        && task.Id == taskId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(task => task.Status, status)
        .SetProperty(task => task.UpdatedAt, updatedAt),
        cancellationToken);

    return updated == 0
      ? null
      : await FindInProjectAsync(
        workspaceId,
        projectId,
        taskId,
        cancellationToken);
  }

  public async Task<bool> DeleteInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    CancellationToken cancellationToken)
  {
    var deleted = await dbContext.Tasks
      .Where(task => task.WorkspaceId == workspaceId
        && task.ProjectId == projectId
        && task.Id == taskId)
      .ExecuteDeleteAsync(cancellationToken);

    return deleted > 0;
  }

  private static ProjectTask ToTask(TaskEntity entity) =>
    new(
      entity.Id,
      entity.ProjectId,
      entity.WorkspaceId,
      entity.Title,
      entity.Description,
      entity.Status,
      entity.Priority,
      entity.CreatedAt,
      entity.UpdatedAt);
}
