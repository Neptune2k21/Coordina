namespace Coordina.Api.Modules.Boards.Contracts;

/// <param name="MaxSize">The biggest card size that fits the user's focus slot, such as S for a quick task; none means any size.</param>
public sealed record BoardPlanningRequest(
  string? MaxSize = null,
  string? Context = null,
  bool? PreferAssigned = null,
  bool? IncludeUnassigned = null);
