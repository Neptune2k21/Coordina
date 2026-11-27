namespace Coordina.Api.Modules.Boards.Domain;

/// <summary>
/// Something that happened to a card, as the database recorded it. Values are
/// codes, ids or dates, depending on the kind: a status change goes from one
/// task status to another, a dependency is added <em>to</em> a card id.
/// </summary>
public sealed record BoardCardEvent(
  long Id,
  Guid CardId,
  BoardCardEventKind Kind,
  string? From,
  string? To,
  DateTimeOffset OccurredAt);

public enum BoardCardEventKind
{
  Created,
  StatusChanged,
  SizeChanged,
  MilestoneChanged,
  AssigneeAdded,
  AssigneeRemoved,
  DependencyAdded,
  DependencyRemoved,
  Deleted
}
