using Coordina.Api.Modules.Workspaces.Infrastructure;
using Coordina.Api.Modules.Projects.Domain;

namespace Coordina.Api.Modules.Projects.Infrastructure;

public sealed class ProjectEntity
{
  public Guid Id { get; set; }
  public required string Name { get; set; }
  public string? Description { get; set; }
  public string? Key { get; set; }
  public string? Icon { get; set; }
  public string? Color { get; set; }
  public Guid WorkspaceId { get; set; }
  public Guid ProjectOwnerId { get; set; }
  public ProjectStatus Status { get; set; } = ProjectStatus.Active;
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }
  public DateTimeOffset? ArchivedAt { get; set; }

  public WorkspaceEntity? Workspace { get; set; }
}
