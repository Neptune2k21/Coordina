using Coordina.Api.Modules.Projects.Domain;

namespace Coordina.Api.Modules.Projects.Application;

public interface IProjectStore
{
  Task<Project> CreateAsync(
    Guid workspaceId,
    string name,
    string? description,
    string? key,
    string? icon,
    string? color,
    Guid projectOwnerId,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  Task<IReadOnlyCollection<Project>> ListForWorkspaceAsync(
    Guid workspaceId,
    bool includeArchived,
    bool includeCompleted,
    CancellationToken cancellationToken);

  Task<Project?> FindInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken);

  Task<Project?> UpdateInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    ProjectUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<bool> ArchiveInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    DateTimeOffset archivedAt,
    CancellationToken cancellationToken);

  Task<bool> PermanentlyDeleteInWorkspaceAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken);
}

public sealed record ProjectUpdate(
  string Name,
  string? Description,
  string? Key,
  string? Icon,
  string? Color,
  Guid ProjectOwnerId,
  ProjectStatus Status);
