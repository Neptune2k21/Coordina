namespace Coordina.Api.Modules.Boards.Application;

public enum BoardResultStatus
{
  Success,
  ValidationError,
  Forbidden,
  NotFound,
  Conflict
}

public sealed record BoardResult<T>(
  BoardResultStatus Status,
  T? Value = default,
  IReadOnlyDictionary<string, string[]>? Errors = null,
  string? Message = null);
