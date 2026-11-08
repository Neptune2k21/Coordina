using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>Changes a list. Fields left out keep their value.</summary>
public sealed record UpdateBoardListRequest(
  string? Title = null,
  [property: EnumCodes<BoardTaskStatus>] string? TaskStatus = null,
  int? Position = null);
