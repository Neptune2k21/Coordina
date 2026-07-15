namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record BoardCardAssigneeResponse(
  Guid UserId,
  string? Name,
  string? Email);
