namespace Coordina.Api.Modules.Tasks.Domain;

public sealed record ProjectBoardCardAssignee(
  Guid UserId,
  string? Name,
  string? Email);
