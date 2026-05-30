using Coordina.Api.Modules.Tasks.Domain;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class BoardCardEntity
{
  public Guid Id { get; set; }
  public Guid BoardId { get; set; }
  public Guid ListId { get; set; }
  public Guid ProjectId { get; set; }
  public Guid WorkspaceId { get; set; }
  public required string Title { get; set; }
  public string? Description { get; set; }
  public BoardCardPriority? Priority { get; set; }
  public DateOnly? DueDate { get; set; }
  public string[] Labels { get; set; } = [];
  public int Position { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }

  public BoardEntity? Board { get; set; }
  public BoardListEntity? List { get; set; }
  public ICollection<BoardCardAssigneeEntity> Assignees { get; set; } = [];
}
