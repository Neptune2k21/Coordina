namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>
/// How to look for a cut: the target in focus hours from now (6 a working
/// day), the chance of shipping by then to aim for, and the search's budget.
/// The same seed gives the same cut, whatever the thread count, unless the
/// <paramref name="TimeLimit"/> stops the search first.
/// </summary>
public sealed record GraphScopeOptions<TNode>(
  TNode Milestone,
  int PersonCount,
  double TargetHours,
  double Goal,
  int RunCount,
  int ScreenRunCount,
  ulong Seed,
  int EvaluationLimit,
  TimeSpan TimeLimit,
  int ThreadCount = 0)
  where TNode : notnull;

/// <summary>
/// Cards to take out of a milestone, in the order the search took them, every
/// card that leaves its scope with them, and the share of simulated runs done
/// by the target before and after.
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
