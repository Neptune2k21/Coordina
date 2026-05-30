namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record UpdateTaskRequest(
  string? Title,
  string? Description,
  string? Priority);
