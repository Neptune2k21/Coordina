using Coordina.Api.Modules.Projects.Infrastructure;
using Coordina.Api.Modules.Tasks.Domain;
using TaskStatus = Coordina.Api.Modules.Tasks.Domain.TaskStatus;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class TaskEntity
{
  public Guid Id { get; set; }
  public Guid ProjectId { get; set; }
  public Guid WorkspaceId { get; set; }
  public required string Title { get; set; }
  public string? Description { get; set; }
  public TaskStatus Status { get; set; } = TaskStatus.Todo;
  public TaskPriority? Priority { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }

  public ProjectEntity? Project { get; set; }
}
