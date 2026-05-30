namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record TaskResponse(
  Guid Id,
  Guid ProjectId,
  string Title,
  string? Description,
  string Status,
  string? Priority,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt);
