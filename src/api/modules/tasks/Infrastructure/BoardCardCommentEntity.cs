namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class BoardCardCommentEntity
{
  public Guid Id { get; set; }
  public Guid CardId { get; set; }
  public Guid UserId { get; set; }
  public required string Body { get; set; }
  public DateTimeOffset CreatedAt { get; set; }

  public BoardCardEntity? Card { get; set; }
}
