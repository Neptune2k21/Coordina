namespace Coordina.Api.Modules.Auth.Application;

public enum AuthResultStatus
{
  Success,
  ValidationError,
  Conflict,
  Unauthorized
}

public sealed record AuthResult<T>(
  AuthResultStatus Status,
  T? Value = default,
  string? Message = null,
  Dictionary<string, string[]>? Errors = null);
