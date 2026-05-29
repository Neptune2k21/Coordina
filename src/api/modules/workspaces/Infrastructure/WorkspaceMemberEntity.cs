using Coordina.Api.Modules.Workspaces.Domain;

namespace Coordina.Api.Modules.Workspaces.Infrastructure;

public sealed class WorkspaceMemberEntity
{
  public Guid Id { get; set; }
  public Guid WorkspaceId { get; set; }
  public Guid UserId { get; set; }
  public WorkspaceRole Role { get; set; }
  public DateTimeOffset JoinedAt { get; set; }

  public WorkspaceEntity? Workspace { get; set; }
}
