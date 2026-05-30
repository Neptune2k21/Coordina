namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record CreateBoardCardRequest(
  string? Title,
  string? Description,
  string? Priority,
  DateOnly? DueDate,
  string[]? Labels,
  Guid[]? AssigneeIds);
