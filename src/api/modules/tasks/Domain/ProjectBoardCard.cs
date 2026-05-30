namespace Coordina.Api.Modules.Tasks.Domain;

public sealed record ProjectBoardCard(
  Guid Id,
  Guid BoardId,
  Guid ListId,
  Guid ProjectId,
  Guid WorkspaceId,
  string Title,
  string? Description,
  BoardCardPriority? Priority,
  DateOnly? DueDate,
  IReadOnlyCollection<string> Labels,
  int Position,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<ProjectBoardCardAssignee> Assignees);
