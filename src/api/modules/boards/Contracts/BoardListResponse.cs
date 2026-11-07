using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record BoardListResponse(
  Guid Id,
  Guid BoardId,
  string Title,
  int Position,
  [property: EnumCodes<BoardTaskStatus>] string TaskStatus,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<BoardCardResponse> Cards);
