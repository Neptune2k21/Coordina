namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record BoardListResponse(
  Guid Id,
  Guid BoardId,
  string Title,
  int Position,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<BoardCardResponse> Cards);
