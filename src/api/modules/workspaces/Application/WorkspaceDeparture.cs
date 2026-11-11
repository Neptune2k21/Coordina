namespace Coordina.Api.Modules.Workspaces.Application;

/// <summary>
/// A member who left or was removed, and the owner who takes over what they
/// owned in the workspace.
/// </summary>
public sealed record WorkspaceDeparture(
  Guid WorkspaceId,
  Guid UserId,
  Guid SuccessorOwnerId);
