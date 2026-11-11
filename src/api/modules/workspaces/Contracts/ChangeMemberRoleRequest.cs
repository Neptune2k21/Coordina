using Coordina.Api.Modules.Workspaces.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Workspaces.Contracts;

public sealed record ChangeMemberRoleRequest(
  [property: EnumCodes<WorkspaceRole>] string Role);
