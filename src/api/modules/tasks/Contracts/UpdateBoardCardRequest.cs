namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record UpdateBoardCardRequest(
  string? Title,
  string? Description,
  string? Priority,
  DateOnly? DueDate,
  bool ClearDueDate,
  string[]? Labels,
  Guid[]? AssigneeIds);
