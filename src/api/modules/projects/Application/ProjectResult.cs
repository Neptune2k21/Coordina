namespace Coordina.Api.Modules.Projects.Application;

public enum ProjectResultStatus
{
  Success,
  ValidationError,
  NotFound,
  Forbidden,
  Conflict
}

public sealed record ProjectResult<T>(
  ProjectResultStatus Status,
  T? Value = default,
  string? Message = null,
  IReadOnlyDictionary<string, string[]>? Errors = null);
