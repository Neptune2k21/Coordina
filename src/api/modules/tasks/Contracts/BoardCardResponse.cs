namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record BoardCardResponse(
  Guid Id,
  Guid BoardId,
  Guid ListId,
  string Title,
  string? Description,
  string? Priority,
  DateOnly? DueDate,
  IReadOnlyCollection<string> Labels,
  int Position,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<BoardCardAssigneeResponse> Assignees);
