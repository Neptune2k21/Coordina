namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record BoardResponse(
  Guid Id,
  Guid ProjectId,
  string Name,
  string Template,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<BoardListResponse> Lists);
