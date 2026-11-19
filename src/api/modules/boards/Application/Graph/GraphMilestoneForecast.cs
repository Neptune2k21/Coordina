namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>A person working on a card; people are numbered from 0 to the team size.</summary>
public sealed record GraphAssignee<TNode>(TNode Node, int Person)
  where TNode : notnull;

/// <summary>What to forecast: the milestone, the team, and how many runs to simulate.</summary>
public sealed record GraphForecastOptions<TNode>(
  TNode Milestone,
  int PersonCount,
  int RunCount,
  ulong Seed)
  where TNode : notnull;

/// <summary>
/// When a milestone's open cards are done, in working days from now: one
/// chance in two (P50) and a realistic commitment (P85).
/// </summary>
public sealed record GraphMilestoneForecast<TNode>(
  double P50Days,
  double P85Days,
  int UnsizedCount,
  IReadOnlyCollection<GraphCardCriticality<TNode>> Cards)
  where TNode : notnull;

/// <summary>The share of runs, from 0 to 1, where the card drives the milestone's date.</summary>
public sealed record GraphCardCriticality<TNode>(TNode Node, double Criticality)
  where TNode : notnull;
