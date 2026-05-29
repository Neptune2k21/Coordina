namespace Coordina.Api.Modules.Workspaces.Domain;

public sealed record Workspace(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  WorkspaceRole CurrentUserRole);
