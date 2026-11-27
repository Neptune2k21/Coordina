namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>When a board's milestones ship, and the plan that says so.</summary>
public sealed record GraphForecast<TNode>(
  IReadOnlyList<GraphMilestoneForecast<TNode>> Milestones,
  IReadOnlyList<GraphPlannedCard<TNode>> Plan)
  where TNode : notnull;

/// <summary>
/// When a milestone's open cards are done, in working days from now, with 10,
/// 50, 85 and 95% chances; the chance of being done by its target and how late
/// it is on average; and its 50% date with as many people as it takes, which
/// no staffing beats.
/// </summary>
public sealed record GraphMilestoneForecast<TNode>(
  TNode Node,
  double P10Days,
  double P50Days,
  double P85Days,
  double P95Days,
  double OnTime,
  double LateDays,
  double ChainDays,
  int CardCount,
  int UnsizedCount,
  IReadOnlyList<GraphCardForecast<TNode>> Cards)
  where TNode : notnull;

/// <summary>
/// How a card weighs on the milestone's date: the share of runs where it drives
/// the date (0 to 1), and how closely its own uncertainty moves it (-1 to 1).
/// </summary>
public sealed record GraphCardForecast<TNode>(TNode Node, double Criticality, double Sensitivity)
  where TNode : notnull;

/// <summary>
/// Who is expected to take a card (null when nobody in particular) and when,
/// in working days from now, if every card takes its median time.
/// </summary>
public sealed record GraphPlannedCard<TNode>(TNode Node, int? Person, double StartDays, double FinishDays)
  where TNode : notnull;
