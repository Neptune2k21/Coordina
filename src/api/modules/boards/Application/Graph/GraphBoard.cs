namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>
/// Everything the engine's planning functions read about a board. Assignees
/// and labels cover completed cards too: they say who did them and what they
/// were, which is what the forecasts learn from.
/// </summary>
public sealed record GraphBoard<TNode>(
  IReadOnlyCollection<TNode> Nodes,
  IReadOnlyCollection<GraphEdge<TNode>> Edges,
  IReadOnlyCollection<GraphWorkItem<TNode>> WorkItems,
  IReadOnlyCollection<GraphAssignee<TNode>> Assignees,
  IReadOnlyCollection<GraphCardLabel<TNode>> Labels,
  IReadOnlyList<GraphMilestone<TNode>> Milestones,
  IReadOnlyCollection<GraphFinishedCard<TNode>> History,
  GraphScopeGrowth Growth,
  int PersonCount,
  int ExtraPeople = 0)
  where TNode : notnull;

/// <summary>A person on a card, open or done; people are numbered from 0.</summary>
public sealed record GraphAssignee<TNode>(TNode Node, int Person)
  where TNode : notnull;

/// <summary>A label of a card, under any number.</summary>
public sealed record GraphCardLabel<TNode>(TNode Node, int Label)
  where TNode : notnull;

/// <summary>
/// A card with a target date: the focus hours (6 a working day) from now to
/// the end of it, and what being late costs against the other milestones.
/// </summary>
public sealed record GraphMilestone<TNode>(TNode Node, double TargetHours, double Weight = 1)
  where TNode : notnull;

/// <summary>The focus hours a completed card took, and how many days ago it was completed.</summary>
public sealed record GraphFinishedCard<TNode>(TNode Node, double Hours, double AgeDays)
  where TNode : notnull;

/// <summary>Cards added to a milestone's scope after its work started, against the cards completed in them.</summary>
public sealed record GraphScopeGrowth(int Discovered, int Finished)
{
  public static GraphScopeGrowth None { get; } = new(0, 0);
}

/// <summary>
/// How to simulate. The same seed gives the same result, whatever the thread
/// count; a <paramref name="ThreadCount"/> of 0 uses the cores other calls
/// leave free.
/// </summary>
public sealed record GraphSimulation(ulong Seed, int RunCount, int ThreadCount = 0);
