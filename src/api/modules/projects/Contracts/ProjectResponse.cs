using Coordina.Api.Modules.Projects.Domain;
using Coordina.Api.Shared.OpenApi;

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
  [property: EnumCodes<ProjectStatus>] string Status,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  DateTimeOffset? ArchivedAt);
