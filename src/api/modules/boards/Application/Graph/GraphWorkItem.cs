namespace Coordina.Api.Modules.Boards.Application.Graph;

public sealed record GraphWorkItem<TNode>(
  TNode Node,
  bool IsCompleted,
  int Priority,
  int DueDays,
  int SubtaskTotal,
  int SubtaskCompleted,
  int Position,
  GraphSize Size = GraphSize.None,
  GraphAssignment Assignment = GraphAssignment.Unassigned,
  bool IsInProgress = false,
  bool IsContextMatch = false)
  where TNode : notnull
{
  public const int NoDueDate = int.MaxValue;
}

public enum GraphAssignment
{
  Unassigned = 0,
  Requester = 1,
  Others = 2
}

/// <summary>Mirrors <c>coordina_graph_size</c>; None means unsized, or no limit in options.</summary>
public enum GraphSize
{
  None = 0,
  Xs = 1,
  S = 2,
  M = 3,
  L = 4,
  Xl = 5,
  Xxl = 6
}

public sealed record GraphPlanningOptions(
  GraphSize MaxSize = GraphSize.None,
  bool PreferAssigned = true,
  bool IncludeUnassigned = true)
{
  public static GraphPlanningOptions Default { get; } = new();
}
