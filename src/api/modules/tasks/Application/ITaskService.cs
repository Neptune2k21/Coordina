using Coordina.Api.Modules.Tasks.Contracts;

namespace Coordina.Api.Modules.Tasks.Application;

public interface ITaskService
{
  Task<TaskResult<TaskResponse>> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    CreateTaskRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<IReadOnlyCollection<TaskResponse>>> ListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<TaskResponse>> UpdateAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    UpdateTaskRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<TaskResponse>> ChangeStatusAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    ChangeTaskStatusRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<TaskResult<object>> DeleteAsync(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    Guid userId,
    CancellationToken cancellationToken);
}
