namespace Coordina.Api.Modules.Workspaces.Infrastructure;

public sealed class WorkspaceInviteEntity
{
  public Guid Id { get; set; }
  public Guid WorkspaceId { get; set; }
  public Guid CreatedByUserId { get; set; }
  public Guid? ConsumedByUserId { get; set; }
  public required string CodeHash { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset ExpiresAt { get; set; }
  public DateTimeOffset? ConsumedAt { get; set; }

  public WorkspaceEntity? Workspace { get; set; }
}
