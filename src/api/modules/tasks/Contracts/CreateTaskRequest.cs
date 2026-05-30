namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record CreateTaskRequest(
  string? Title,
  string? Description,
  string? Priority);
