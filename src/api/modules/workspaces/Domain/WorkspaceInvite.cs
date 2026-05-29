namespace Coordina.Api.Modules.Workspaces.Domain;

public sealed record WorkspaceInvite(
  Guid Id,
  Guid WorkspaceId,
  DateTimeOffset ExpiresAt,
  DateTimeOffset? ConsumedAt);
