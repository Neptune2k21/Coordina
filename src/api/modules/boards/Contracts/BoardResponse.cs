using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record BoardResponse(
  Guid Id,
  Guid ProjectId,
  string Name,
  [property: EnumCodes<BoardTemplate>] string Template,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<BoardListResponse> Lists);
