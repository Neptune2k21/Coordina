namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record CreateBoardRequest(
  string? Name,
  string? Template,
  string[]? CustomListTitles);
