namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record UpdateBoardCardSubtaskRequest(
  string? Title,
  bool? IsCompleted);
