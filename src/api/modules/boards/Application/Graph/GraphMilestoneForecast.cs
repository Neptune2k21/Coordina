namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>A person working on a card; people are numbered from 0 to the team size.</summary>
public sealed record GraphAssignee<TNode>(TNode Node, int Person)
  where TNode : notnull;

/// <summary>What to forecast; a <paramref name="ThreadCount"/> of 0 uses every core, with the same result.</summary>
public sealed record GraphForecastOptions<TNode>(
  TNode Milestone,
  int PersonCount,
  int RunCount,
  ulong Seed,
  int ThreadCount = 0)
  where TNode : notnull;

/// <summary>
/// When a milestone's open cards are done, in working days from now: one
/// chance in two (P50) and a realistic commitment (P85).
/// </summary>
public sealed record GraphMilestoneForecast<TNode>(
  double P50Days,
  double P85Days,
  int UnsizedCount,
  IReadOnlyCollection<GraphCardForecast<TNode>> Cards)
  where TNode : notnull;

/// <summary>
/// How a card weighs on the milestone's date: the share of runs where it drives
/// the date (0 to 1), and how closely its duration moves the date (-1 to 1).
/// </summary>
public sealed record GraphCardForecast<TNode>(TNode Node, double Criticality, double Sensitivity)
  where TNode : notnull;
