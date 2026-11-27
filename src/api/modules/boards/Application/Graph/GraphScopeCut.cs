namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>
/// How to look for a cut of one of the board's milestones: the chance of
/// shipping by its target to aim for, and the search's budget. Only the
/// <paramref name="TimeLimit"/> makes the result depend on the machine.
/// </summary>
public sealed record GraphScopeOptions<TNode>(
  TNode Milestone,
  GraphSimulation Simulation,
  double Goal,
  int ScreenRunCount,
  int EvaluationLimit,
  TimeSpan TimeLimit)
  where TNode : notnull;

/// <summary>
/// Cards to take out of a milestone, in the order the search took them, every
/// card that leaves its scope with them, and the milestone's forecast now and
/// once the cut is applied.
/// </summary>
public sealed record GraphScopeCut<TNode>(
  IReadOnlyList<TNode> Cuts,
  IReadOnlyCollection<GraphDroppedCard<TNode>> Dropped,
  double OnTimeBefore,
  double OnTimeAfter,
  double P50Days,
  double P85Days)
  where TNode : notnull;

/// <summary>A card that leaves the scope, and the index in the cuts of the cut that takes it out.</summary>
public sealed record GraphDroppedCard<TNode>(TNode Node, int Cut)
  where TNode : notnull;
