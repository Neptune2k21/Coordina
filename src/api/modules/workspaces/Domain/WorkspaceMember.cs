namespace Coordina.Api.Modules.Workspaces.Domain;

public sealed record WorkspaceMember(
  Guid UserId,
  string? Name,
  string? Email,
  WorkspaceRole Role,
  DateTimeOffset JoinedAt);
