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

/// <summary>A card that drives the milestone's date in a share of the simulated runs, from 0 to 1.</summary>
public sealed record BoardMilestoneDriverResponse(
  Guid CardId,
  string Title,
  double Criticality);
