namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardCardAssigneeEntity
{
  public Guid Id { get; set; }
  public Guid CardId { get; set; }
  public Guid UserId { get; set; }
  public DateTimeOffset AssignedAt { get; set; }

  public BoardCardEntity? Card { get; set; }
}
