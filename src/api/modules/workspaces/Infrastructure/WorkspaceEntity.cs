namespace Coordina.Api.Modules.Workspaces.Infrastructure;

public sealed class WorkspaceEntity
{
  public Guid Id { get; set; }
  public required string Name { get; set; }
  public DateTimeOffset CreatedAt { get; set; }

  public ICollection<WorkspaceMemberEntity> Members { get; set; } = [];
  public ICollection<WorkspaceInviteEntity> Invites { get; set; } = [];
}
