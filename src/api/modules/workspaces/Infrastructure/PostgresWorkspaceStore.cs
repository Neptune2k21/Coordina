using Coordina.Api.Infrastructure.Persistence;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Workspaces.Infrastructure;

public sealed class PostgresWorkspaceStore(CoordinaDbContext dbContext)
  : IWorkspaceStore
{
  public async Task<Workspace> CreateAsync(
    string name,
    Guid ownerUserId,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var entity = new WorkspaceEntity
    {
      Id = Guid.NewGuid(),
      Name = name,
      CreatedAt = createdAt,
      Members =
      [
        new WorkspaceMemberEntity
        {
          Id = Guid.NewGuid(),
          UserId = ownerUserId,
          Role = WorkspaceRole.Owner,
          JoinedAt = createdAt
        }
      ]
    };

    dbContext.Workspaces.Add(entity);
    await dbContext.SaveChangesAsync(cancellationToken);

    return new Workspace(
      entity.Id,
      entity.Name,
      entity.CreatedAt,
      WorkspaceRole.Owner);
  }

  public async Task<IReadOnlyCollection<Workspace>> ListForUserAsync(
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await dbContext.WorkspaceMembers
      .AsNoTracking()
      .Where(member => member.UserId == userId)
      .OrderBy(member => member.Workspace!.Name)
      .Select(member => new Workspace(
        member.WorkspaceId,
        member.Workspace!.Name,
        member.Workspace.CreatedAt,
        member.Role))
      .ToArrayAsync(cancellationToken);
  }

  public async Task<Workspace?> FindForUserAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await dbContext.WorkspaceMembers
      .AsNoTracking()
      .Where(member => member.WorkspaceId == workspaceId
        && member.UserId == userId)
      .Select(member => new Workspace(
        member.WorkspaceId,
        member.Workspace!.Name,
        member.Workspace.CreatedAt,
        member.Role))
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<WorkspaceRole?> FindUserRoleAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await dbContext.WorkspaceMembers
      .AsNoTracking()
      .Where(member => member.WorkspaceId == workspaceId
        && member.UserId == userId)
      .Select(member => (WorkspaceRole?)member.Role)
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<Workspace?> FindByIdAsync(
    Guid workspaceId,
    CancellationToken cancellationToken)
  {
    return await dbContext.Workspaces
      .AsNoTracking()
      .Where(workspace => workspace.Id == workspaceId)
      .Select(workspace => new Workspace(
        workspace.Id,
        workspace.Name,
        workspace.CreatedAt,
        WorkspaceRole.Member))
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<IReadOnlyCollection<WorkspaceMember>> ListMembersAsync(
    Guid workspaceId,
    CancellationToken cancellationToken)
  {
    return await dbContext.WorkspaceMembers
      .AsNoTracking()
      .Where(member => member.WorkspaceId == workspaceId)
      .Join(
        dbContext.AuthUsers.AsNoTracking(),
        member => member.UserId,
        user => user.Id,
        (member, user) => new
        {
          member.UserId,
          user.Name,
          user.Email,
          member.Role,
          member.JoinedAt
        })
      .OrderBy(member => member.Role == WorkspaceRole.Owner ? 0 : 1)
      .ThenBy(member => member.Name)
      .Select(member => new WorkspaceMember(
        member.UserId,
        member.Name,
        member.Email,
        member.Role,
        member.JoinedAt))
      .ToArrayAsync(cancellationToken);
  }

  public async Task<Workspace> AddMemberAsync(
    Guid workspaceId,
    Guid userId,
    DateTimeOffset joinedAt,
    CancellationToken cancellationToken)
  {
    dbContext.WorkspaceMembers.Add(new WorkspaceMemberEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      UserId = userId,
      Role = WorkspaceRole.Member,
      JoinedAt = joinedAt
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    var workspace = await FindForUserAsync(
      workspaceId,
      userId,
      cancellationToken);

    return workspace ?? throw new InvalidOperationException(
      "Workspace membership was not created.");
  }

  public async Task<WorkspaceInvite> CreateInviteAsync(
    Guid workspaceId,
    Guid createdByUserId,
    string codeHash,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt,
    CancellationToken cancellationToken)
  {
    var entity = new WorkspaceInviteEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      CreatedByUserId = createdByUserId,
      CodeHash = codeHash,
      CreatedAt = createdAt,
      ExpiresAt = expiresAt
    };

    dbContext.WorkspaceInvites.Add(entity);
    await dbContext.SaveChangesAsync(cancellationToken);

    return new WorkspaceInvite(
      entity.Id,
      entity.WorkspaceId,
      entity.ExpiresAt,
      entity.ConsumedAt);
  }

  public async Task<WorkspaceInvite?> FindUsableInviteAsync(
    string codeHash,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    return await dbContext.WorkspaceInvites
      .AsNoTracking()
      .Where(invite => invite.CodeHash == codeHash
        && invite.ConsumedAt == null
        && invite.ExpiresAt > now)
      .Select(invite => new WorkspaceInvite(
        invite.Id,
        invite.WorkspaceId,
        invite.ExpiresAt,
        invite.ConsumedAt))
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<Workspace> ConsumeInviteAsync(
    Guid inviteId,
    Guid userId,
    DateTimeOffset consumedAt,
    CancellationToken cancellationToken)
  {
    var invite = await dbContext.WorkspaceInvites
      .AsNoTracking()
      .SingleOrDefaultAsync(invite => invite.Id == inviteId,
        cancellationToken);

    if (invite is null)
    {
      throw new InvalidOperationException("Invitation code is no longer usable.");
    }

    var consumed = await dbContext.WorkspaceInvites
      .Where(candidate => candidate.Id == inviteId
        && candidate.ConsumedAt == null
        && candidate.ExpiresAt > consumedAt)
      .ExecuteUpdateAsync(updates => updates
        .SetProperty(candidate => candidate.ConsumedAt, consumedAt)
        .SetProperty(candidate => candidate.ConsumedByUserId, userId),
        cancellationToken);

    if (consumed == 0)
    {
      throw new InvalidOperationException("Invitation code is no longer usable.");
    }

    dbContext.WorkspaceMembers.Add(new WorkspaceMemberEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = invite.WorkspaceId,
      UserId = userId,
      Role = WorkspaceRole.Member,
      JoinedAt = consumedAt
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    var workspace = await FindForUserAsync(
      invite.WorkspaceId,
      userId,
      cancellationToken);

    return workspace ?? throw new InvalidOperationException(
      "Workspace membership was not created.");
  }

  public async Task<bool> RemoveMemberAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var deleted = await dbContext.WorkspaceMembers
      .Where(member => member.WorkspaceId == workspaceId
        && member.UserId == userId
        && member.Role != WorkspaceRole.Owner)
      .ExecuteDeleteAsync(cancellationToken);

    return deleted > 0;
  }

  public async Task DeleteAsync(
    Guid workspaceId,
    CancellationToken cancellationToken)
  {
    await dbContext.Workspaces
      .Where(workspace => workspace.Id == workspaceId)
      .ExecuteDeleteAsync(cancellationToken);
  }
}
