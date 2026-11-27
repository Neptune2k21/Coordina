namespace Coordina.Api.Modules.Boards.Application.Graph;

// Passing a board to the planning functions.
public sealed partial class NativeGuidGraphEngine
{
  // Runs a planning function with the board's arrays and the output buffers
  // pinned; the call gets the buffers' addresses.
  private static void Run(string operation, GraphBoard<Guid> board, Array[] buffers, PlanCall call)
  {
    ArgumentNullException.ThrowIfNull(board);

    var arrays = BoardArrays.From(board);
    var status = WithPinned(
      [.. arrays.All, .. buffers],
      pointers =>
      {
        var native = arrays.ToNative(board, pointers);
        return call(ref native, pointers[arrays.All.Length..]);
      });

    ThrowIfFailed(operation, status);
  }

  private static NativeGraphSimulation ToNative(GraphSimulation simulation)
  {
    ArgumentNullException.ThrowIfNull(simulation);

    return new NativeGraphSimulation(
      simulation.Seed,
      checked((uint)simulation.RunCount),
      checked((uint)simulation.ThreadCount));
  }

  private static uint[] ToNativePeople(IReadOnlyCollection<int> people)
  {
    ArgumentNullException.ThrowIfNull(people);

    return people.Select(person => checked((uint)person)).ToArray();
  }

  private static NativeGraphAssignee[] ToNativeAssignees(IReadOnlyCollection<GraphAssignee<Guid>> assignees)
  {
    ArgumentNullException.ThrowIfNull(assignees);

    return assignees
      .Select(assignee => new NativeGraphAssignee(ToNative(assignee.Node), checked((uint)assignee.Person), 0U))
      .ToArray();
  }

  private sealed record BoardArrays(
    NativeGraphNodeId[] Nodes,
    NativeGraphEdge[] Edges,
    NativeGraphWorkItem[] WorkItems,
    NativeGraphAssignee[] Assignees,
    NativeGraphCardLabel[] Labels,
    NativeGraphMilestone[] Milestones,
    NativeGraphFinishedCard[] History)
  {
    public Array[] All => [Nodes, Edges, WorkItems, Assignees, Labels, Milestones, History];

    public static BoardArrays From(GraphBoard<Guid> board) =>
      new(
        ToNativeNodes(board.Nodes),
        ToNativeEdges(board.Edges),
        ToNativeWorkItems(board.WorkItems),
        ToNativeAssignees(board.Assignees),
        board.Labels
          .Select(label => new NativeGraphCardLabel(
            NativeGuidGraphEngine.ToNative(label.Node),
            checked((uint)label.Label),
            0U))
          .ToArray(),
        board.Milestones
          .Select(milestone => new NativeGraphMilestone(
            NativeGuidGraphEngine.ToNative(milestone.Node),
            milestone.TargetHours,
            milestone.Weight))
          .ToArray(),
        board.History
          .Select(card => new NativeGraphFinishedCard(
            NativeGuidGraphEngine.ToNative(card.Node),
            card.Hours,
            card.AgeDays))
          .ToArray());

    public NativeGraphBoard ToNative(GraphBoard<Guid> board, IntPtr[] pointers) =>
      new()
      {
        Nodes = pointers[0],
        NodeCount = ToNativeCount(Nodes.Length),
        Edges = pointers[1],
        EdgeCount = ToNativeCount(Edges.Length),
        WorkItems = pointers[2],
        WorkItemCount = ToNativeCount(WorkItems.Length),
        Assignees = pointers[3],
        AssigneeCount = ToNativeCount(Assignees.Length),
        Labels = pointers[4],
        LabelCount = ToNativeCount(Labels.Length),
        Milestones = pointers[5],
        MilestoneCount = ToNativeCount(Milestones.Length),
        History = pointers[6],
        HistoryCount = ToNativeCount(History.Length),
        PersonCount = checked((uint)board.PersonCount),
        DiscoveredCount = checked((uint)board.Growth.Discovered),
        FinishedCount = checked((uint)board.Growth.Finished),
        ExtraPeople = checked((uint)board.ExtraPeople)
      };
  }
}
