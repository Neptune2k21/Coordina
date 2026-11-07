using Coordina.Api.Modules.Workspaces.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Workspaces.Contracts;

public sealed record WorkspaceResponse(
  string Id,
  string Name,
  [property: EnumCodes<WorkspaceRole>] string Role,
  DateTimeOffset CreatedAt);
