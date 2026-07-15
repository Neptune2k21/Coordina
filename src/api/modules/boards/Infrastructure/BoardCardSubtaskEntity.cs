namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class BoardCardSubtaskEntity
{
  public Guid Id { get; set; }
  public Guid CardId { get; set; }
  public required string Title { get; set; }
  public bool IsCompleted { get; set; }
  public DateTimeOffset? CompletedAt { get; set; }
  public Guid? CompletedByUserId { get; set; }
  public int Position { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }

  public BoardCardEntity? Card { get; set; }
}
