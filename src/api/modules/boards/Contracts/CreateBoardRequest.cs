using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record CreateBoardRequest(
  string? Name,
  [property: EnumCodes<BoardTemplate>] string? Template,
  string[]? CustomListTitles,
  bool IncludeExampleCards = true);
