namespace Coordina.Api.Modules.Tasks.Application;

public enum TaskResultStatus
{
  Success,
  ValidationError,
  Forbidden,
  NotFound,
  Conflict
}

public sealed record TaskResult<T>(
  TaskResultStatus Status,
  T? Value = default,
  IReadOnlyDictionary<string, string[]>? Errors = null,
  string? Message = null);
