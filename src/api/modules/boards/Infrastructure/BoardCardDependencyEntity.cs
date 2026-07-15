namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardCardDependencyEntity
{
  public Guid Id { get; set; }
  public Guid CardId { get; set; }
  public Guid DependsOnCardId { get; set; }
  public DateTimeOffset CreatedAt { get; set; }

  public BoardCardEntity? Card { get; set; }
}
