using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>
/// A milestone card and when its work is forecast to be done: one chance in
/// two (<paramref name="ForecastDate"/>), 85% (<paramref name="CommitDate"/>),
/// 10% and 95% (best and worst case). <paramref name="UnlimitedTeamDate"/> is
/// the forecast with as many people as it takes, which no staffing beats, and
/// <paramref name="OnTime"/> the chance of being done by the target, from 0 to
/// 1. A completed milestone has no forecast.
/// </summary>
public sealed record BoardMilestoneResponse(
  Guid CardId,
  string Title,
  DateOnly TargetDate,
  [property: EnumCodes<MilestoneStatus>] string Status,
  DateOnly? ForecastDate,
  DateOnly? CommitDate,
  DateOnly? BestCaseDate,
  DateOnly? WorstCaseDate,
  DateOnly? UnlimitedTeamDate,
  double? OnTime,
  int? SlipDays,
  int OpenCardCount,
  int UnsizedCardCount,
  IReadOnlyCollection<BoardMilestoneDriverResponse> Drivers);

/// <summary>
/// A card that drives the milestone's date: the share of simulated runs where
/// it sets the date (0 to 1), and how closely its own uncertainty moves the
/// date (-1 to 1): squared, the share of the date's spread that sizing it
/// better or splitting it can remove.
/// </summary>
public sealed record BoardMilestoneDriverResponse(
  Guid CardId,
  string Title,
  double Criticality,
  double Sensitivity);
