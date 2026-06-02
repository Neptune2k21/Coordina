namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record UpdateBoardCardSubtaskRequest(
  string? Title,
  bool? IsCompleted);
