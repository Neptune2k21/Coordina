using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record CreateBoardListRequest(
  string? Title,
  [property: EnumCodes<BoardTaskStatus>] string? TaskStatus = null);
