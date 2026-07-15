namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record CreateBoardRequest(
  string? Name,
  string? Template,
  string[]? CustomListTitles);
