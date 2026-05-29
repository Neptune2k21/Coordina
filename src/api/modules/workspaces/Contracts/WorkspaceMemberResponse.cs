namespace Coordina.Api.Modules.Workspaces.Contracts;

public sealed record WorkspaceMemberResponse(
  string UserId,
  string? Name,
  string? Email,
  string Role,
  DateTimeOffset JoinedAt);
