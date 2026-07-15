namespace Coordina.Api.Modules.Boards.Domain;

public sealed record ProjectBoardCardAssignee(
  Guid UserId,
  string? Name,
  string? Email);
