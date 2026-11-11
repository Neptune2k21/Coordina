using Coordina.Api.Modules.Workspaces.Domain;

namespace Coordina.Api.Modules.Workspaces.Application;

public interface IWorkspaceStore
{
  /// <summary>
  /// Runs <paramref name="action"/> as one transaction while no other caller
  /// holds this lock for the workspace, so a rule that counts owners and the
  /// membership change that depends on it cannot interleave with another one.
  /// </summary>
  Task<T> RunExclusiveAsync<T>(
    Guid workspaceId,
    Func<CancellationToken, Task<T>> action,
    CancellationToken cancellationToken);

  Task<Workspace> CreateAsync(
    string name,
    Guid ownerUserId,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken);

  Task<IReadOnlyCollection<Workspace>> ListForUserAsync(
    Guid userId,
    CancellationToken cancellationToken);

  Task<Workspace?> FindForUserAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<WorkspaceRole?> FindUserRoleAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<Workspace?> FindByIdAsync(
    Guid workspaceId,
    CancellationToken cancellationToken);

  Task<IReadOnlyCollection<WorkspaceMember>> ListMembersAsync(
    Guid workspaceId,
    CancellationToken cancellationToken);

  Task<Workspace> AddMemberAsync(
    Guid workspaceId,
    Guid userId,
    DateTimeOffset joinedAt,
    CancellationToken cancellationToken);

  Task<WorkspaceInvite> CreateInviteAsync(
    Guid workspaceId,
    Guid createdByUserId,
    string codeHash,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt,
    CancellationToken cancellationToken);

  Task<WorkspaceInvite?> FindUsableInviteAsync(
    string codeHash,
    DateTimeOffset now,
    CancellationToken cancellationToken);

  Task<ConsumeInviteOutcome> ConsumeInviteAsync(
    Guid inviteId,
    Guid userId,
    DateTimeOffset consumedAt,
    CancellationToken cancellationToken);

  Task<bool> UpdateMemberRoleAsync(
    Guid workspaceId,
    Guid userId,
    WorkspaceRole role,
    CancellationToken cancellationToken);

  /// <summary>Removes a membership of any role; the service decides who may leave.</summary>
  Task<bool> RemoveMemberAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);

  Task DeleteAsync(Guid workspaceId, CancellationToken cancellationToken);
}

public enum ConsumeInviteStatus
{
  Consumed,
  Unavailable,
  AlreadyMember
}

public sealed record ConsumeInviteOutcome(
  ConsumeInviteStatus Status,
  Workspace? Workspace = null);
