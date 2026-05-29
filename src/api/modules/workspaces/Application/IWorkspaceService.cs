using Coordina.Api.Modules.Workspaces.Contracts;

namespace Coordina.Api.Modules.Workspaces.Application;

public interface IWorkspaceService
{
  Task<WorkspaceResult<WorkspaceResponse>> CreateAsync(
    CreateWorkspaceRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<IReadOnlyCollection<WorkspaceResponse>> ListAsync(
    Guid userId,
    CancellationToken cancellationToken);

  Task<WorkspaceResult<WorkspaceResponse>> GetAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<WorkspaceResult<WorkspaceResponse>> JoinAsync(
    JoinWorkspaceRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  Task<WorkspaceResult<CreateWorkspaceInviteResponse>> CreateInviteAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<WorkspaceResult<IReadOnlyCollection<WorkspaceMemberResponse>>> ListMembersAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<WorkspaceResult<object>> RemoveMemberAsync(
    Guid workspaceId,
    Guid memberUserId,
    Guid currentUserId,
    CancellationToken cancellationToken);

  Task<WorkspaceResult<object>> DeleteAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);
}
