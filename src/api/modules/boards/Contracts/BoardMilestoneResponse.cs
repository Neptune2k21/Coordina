using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>
/// A milestone card and when its work is forecast to be done. A completed
/// milestone has no forecast.
/// </summary>
public sealed record BoardMilestoneResponse(
  Guid CardId,
  string Title,
  DateOnly TargetDate,
  [property: EnumCodes<MilestoneStatus>] string Status,
  DateOnly? ForecastDate,
  DateOnly? CommitDate,
  int? SlipDays,
  int OpenCardCount,
  int UnsizedCardCount,
  IReadOnlyCollection<BoardMilestoneDriverResponse> Drivers);

/// <summary>
/// A card that drives the milestone's date: the share of simulated runs where
/// it sets the date (0 to 1), and how closely its duration moves the date
/// (-1 to 1), high when sizing it better or splitting it would firm the date up.
/// </summary>
public sealed record BoardMilestoneDriverResponse(
  Guid CardId,
  string Title,
  double Criticality,
  double Sensitivity);
