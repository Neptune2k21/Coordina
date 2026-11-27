namespace Coordina.Api.Modules.Boards.Domain;

/// <summary>
/// What a milestone's forecast said one day: kept so that, once the milestone
/// is done, anyone can check whether its dates were right.
/// </summary>
public sealed record ForecastSnapshot(
  Guid MilestoneId,
  DateOnly TakenOn,
  DateOnly TargetDate,
  DateOnly ForecastDate,
  DateOnly CommitDate,
  double OnTime,
  int OpenCardCount);
