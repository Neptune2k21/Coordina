namespace Coordina.Api.Modules.Workspaces.Application;

public enum WorkspaceResultStatus
{
  Success,
  ValidationError,
  NotFound,
  Forbidden,
  Conflict
}

public sealed record WorkspaceResult<T>(
  WorkspaceResultStatus Status,
  T? Value = default,
  string? Message = null,
  IReadOnlyDictionary<string, string[]>? Errors = null);
