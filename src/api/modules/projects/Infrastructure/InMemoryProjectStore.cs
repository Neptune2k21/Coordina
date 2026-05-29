using System.Collections.Concurrent;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Projects.Domain;

namespace Coordina.Api.Modules.Projects.Infrastructure;

public sealed class InMemoryProjectStore : IProjectStore
{
  private readonly ConcurrentDictionary<Guid, ProjectRecord> _projects = new();

  public Task<Project> CreateAsync(
    Guid workspaceId,
    string name,
    string? description,
    string? key,
    string? icon,
    string? color,
    Guid projectOwnerId,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var record = new ProjectRecord(
      Guid.NewGuid(),
      name,
      description,
      key,
      icon,
      color,
      workspaceId,
      projectOwnerId,
      ProjectStatus.Active,
      createdAt,
      createdAt,
      null);

    _projects[record.Id] = record;

    return Task.FromResult(ToProject(record));
  }

  public Task<IReadOnlyCollection<Project>> ListForWorkspaceAsync(
    Guid workspaceId,
    bool includeArchived,
    bool includeCompleted,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var projects = _projects.Values
      .Where(project => project.WorkspaceId == workspaceId)
      .Where(project => includeArchived || project.Status != ProjectStatus.Archived)
      .Where(project => includeCompleted || project.Status != ProjectStatus.Completed)
      .OrderBy(project => project.Name)
      .Select(ToProject)
      .ToArray();

    return Task.FromResult<IReadOnlyCollection<Project>>(projects);
  }

  public Task<Project?> FindInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_projects.TryGetValue(projectId, out var project)
      || project.WorkspaceId != workspaceId)
    {
      return Task.FromResult<Project?>(null);
    }

    return Task.FromResult<Project?>(ToProject(project));
  }

  public Task<Project?> UpdateInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    ProjectUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_projects.TryGetValue(projectId, out var project)
      || project.WorkspaceId != workspaceId)
    {
      return Task.FromResult<Project?>(null);
    }

    var updated = project with
    {
      Name = update.Name,
      Description = update.Description,
      Key = update.Key,
      Icon = update.Icon,
      Color = update.Color,
      ProjectOwnerId = update.ProjectOwnerId,
      Status = update.Status,
      UpdatedAt = updatedAt,
      ArchivedAt = update.Status == ProjectStatus.Archived ? updatedAt : null
    };

    _projects[projectId] = updated;

    return Task.FromResult<Project?>(ToProject(updated));
  }

  public Task<bool> ArchiveInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    DateTimeOffset archivedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_projects.TryGetValue(projectId, out var project)
      || project.WorkspaceId != workspaceId)
    {
      return Task.FromResult(false);
    }

    _projects[projectId] = project with
    {
      Status = ProjectStatus.Archived,
      UpdatedAt = archivedAt,
      ArchivedAt = archivedAt
    };

    return Task.FromResult(true);
  }

  public Task<bool> PermanentlyDeleteInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!_projects.TryGetValue(projectId, out var project)
      || project.WorkspaceId != workspaceId)
    {
      return Task.FromResult(false);
    }

    return Task.FromResult(_projects.TryRemove(projectId, out _));
  }

  private static Project ToProject(ProjectRecord record) =>
    new(
      record.Id,
      record.Name,
      record.Description,
      record.Key,
      record.Icon,
      record.Color,
      record.WorkspaceId,
      record.ProjectOwnerId,
      null,
      record.Status,
      record.CreatedAt,
      record.UpdatedAt,
      record.ArchivedAt);

  private sealed record ProjectRecord(
    Guid Id,
    string Name,
    string? Description,
    string? Key,
    string? Icon,
    string? Color,
    Guid WorkspaceId,
    Guid ProjectOwnerId,
    ProjectStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt);
}
