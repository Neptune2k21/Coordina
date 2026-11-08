namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>How the next-work ranking is computed for one user on one board.</summary>
public sealed record BoardPlanningPreferencesResponse(
  int AvailableMinutes,
  string? Context,
  bool PreferAssigned,
  bool IncludeUnassigned);
