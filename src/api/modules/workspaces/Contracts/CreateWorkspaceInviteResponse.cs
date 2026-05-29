namespace Coordina.Api.Modules.Workspaces.Contracts;

public sealed record CreateWorkspaceInviteResponse(
  string Code,
  DateTimeOffset ExpiresAt);
