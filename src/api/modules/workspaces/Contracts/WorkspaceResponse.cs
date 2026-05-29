namespace Coordina.Api.Modules.Workspaces.Contracts;

public sealed record WorkspaceResponse(
  string Id,
  string Name,
  string Role,
  DateTimeOffset CreatedAt);
