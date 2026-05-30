using Coordina.Api.Modules.Tasks.Domain;
using TaskStatus = Coordina.Api.Modules.Tasks.Domain.TaskStatus;

namespace Coordina.Api.Modules.Tasks.Application;

public interface ITaskStore
{
  Task<ProjectTask> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    string title,
    string? description,
    TaskPriority? priority,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  Task<IReadOnlyCollection<ProjectTask>> ListForProjectAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken);

  Task<ProjectTask?> FindInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    CancellationToken cancellationToken);

  Task<ProjectTask?> UpdateInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    TaskUpdate update,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<ProjectTask?> ChangeStatusInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    TaskStatus status,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken);

  Task<bool> DeleteInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    CancellationToken cancellationToken);
}

public sealed record TaskUpdate(
  string Title,
  string? Description,
  TaskPriority? Priority);
