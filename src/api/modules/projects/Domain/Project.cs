namespace Coordina.Api.Modules.Projects.Domain;

public sealed record Project(
  Guid Id,
  string Name,
  string? Description,
  string? Key,
  string? Icon,
  string? Color,
  Guid WorkspaceId,
  Guid ProjectOwnerId,
  string? ProjectOwnerName,
  ProjectStatus Status,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  DateTimeOffset? ArchivedAt);
