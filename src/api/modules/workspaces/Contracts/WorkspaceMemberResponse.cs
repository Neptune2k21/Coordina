using Coordina.Api.Modules.Workspaces.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Workspaces.Contracts;

public sealed record WorkspaceMemberResponse(
  string UserId,
  string? Name,
  string? Email,
  [property: EnumCodes<WorkspaceRole>] string Role,
  DateTimeOffset JoinedAt);
