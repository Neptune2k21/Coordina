namespace Coordina.Api.Modules.Tasks.Domain;

public sealed record ProjectTask(
  Guid Id,
  Guid ProjectId,
  Guid WorkspaceId,
  string Title,
  string? Description,
  TaskStatus Status,
  TaskPriority? Priority,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt);
