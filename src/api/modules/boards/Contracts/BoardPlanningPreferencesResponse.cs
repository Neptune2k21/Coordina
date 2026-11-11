using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>How the next-work ranking is computed for one user on one board.</summary>
public sealed record BoardPlanningPreferencesResponse(
  [property: EnumCodes<CardSize>] string? MaxSize,
  string? Context,
  bool PreferAssigned,
  bool IncludeUnassigned);
