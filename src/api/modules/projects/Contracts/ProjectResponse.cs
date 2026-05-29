namespace Coordina.Api.Modules.Projects.Contracts;

public sealed record ProjectResponse(
  Guid Id,
  string Name,
  string? Description,
  string? Key,
  string? Icon,
  string? Color,
  Guid WorkspaceId,
  Guid ProjectOwnerId,
  string? ProjectOwnerName,
  string Status,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  DateTimeOffset? ArchivedAt);
