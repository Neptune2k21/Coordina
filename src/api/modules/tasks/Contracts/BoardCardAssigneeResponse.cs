namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record BoardCardAssigneeResponse(
  Guid UserId,
  string? Name,
  string? Email);
