using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardCardEventEntity
{
  public long Id { get; set; }
  public Guid BoardId { get; set; }
  public Guid CardId { get; set; }
  public BoardCardEventKind Kind { get; set; }
  public string? FromValue { get; set; }
  public string? ToValue { get; set; }
  public DateTimeOffset OccurredAt { get; set; }

  public BoardEntity? Board { get; set; }
}
