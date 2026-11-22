namespace Coordina.Api.Modules.Boards.Application.Graph;

// What to take out of a milestone so it ships on time.
public sealed partial class NativeGuidGraphEngine
{
  private delegate NativeGraphStatus ScopeCall(
    NativeGraphNodeId[] nodes,
    UIntPtr nodeCount,
    NativeGraphEdge[] edges,
    UIntPtr edgeCount,
    NativeGraphWorkItem[] workItems,
    UIntPtr workItemCount,
    NativeGraphAssignee[] assignees,
    UIntPtr assigneeCount,
    NativeGraphNodeId[] cards,
    UIntPtr cardCount,
    ref NativeGraphScopeOptions options,
    ref NativeGraphScopeCut output);

  public GraphScopeCut<Guid> ProposeScope(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    IReadOnlyCollection<GraphAssignee<Guid>> assignees,
    IReadOnlyCollection<Guid> keep,
    GraphScopeOptions<Guid> options) =>
    RunScope(
      new ScopeInput(nodes, edges, workItems, assignees, keep, options),
      NativeMethods.ProposeScope,
      "propose_scope");

  public GraphScopeCut<Guid> EvaluateScope(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    IReadOnlyCollection<GraphAssignee<Guid>> assignees,
    IReadOnlyCollection<Guid> cuts,
    GraphScopeOptions<Guid> options) =>
    RunScope(
      new ScopeInput(nodes, edges, workItems, assignees, cuts, options),
      NativeMethods.EvaluateScope,
      "evaluate_scope");

  private static GraphScopeCut<Guid> RunScope(ScopeInput input, ScopeCall call, string operation)
  {
    var graph = MarshalledGraph.From(input.Nodes, input.Edges, input.WorkItems);
    ArgumentNullException.ThrowIfNull(input.Assignees);
    ArgumentNullException.ThrowIfNull(input.Cards);
    ArgumentNullException.ThrowIfNull(input.Options);

    var assignees = ToNativeAssignees(input.Assignees);
    var cards = ToNativeNodes(input.Cards);
    var options = ToNativeOptions(input.Options);
    // A card that leaves the scope is a node, so the node count always fits.
    var cuts = new NativeGraphNodeId[graph.Nodes.Length];
    var dropped = new NativeGraphDroppedCard[graph.Nodes.Length];
    var output = new NativeGraphScopeCut
    {
      CutCapacity = ToNativeCount(cuts.Length),
      DroppedCapacity = ToNativeCount(dropped.Length)
    };

    var status = WithPinned(
      [cuts, dropped],
      pointers =>
      {
        output.Cuts = pointers[0];
        output.Dropped = pointers[1];
        return call(
          graph.Nodes,
          graph.NodeCount,
          graph.Edges,
          graph.EdgeCount,
          graph.WorkItems,
          graph.WorkItemCount,
          assignees,
          ToNativeCount(assignees.Length),
          cards,
          ToNativeCount(cards.Length),
          ref options,
          ref output);
      });

    if (status != NativeGraphStatus.Ok)
    {
      throw new GraphEngineException(operation, (int)status);
    }

    return new GraphScopeCut<Guid>(
      Written(cuts, output.CutCount, FromNative),
      Written(
        dropped,
        output.DroppedCount,
        card => new GraphDroppedCard<Guid>(FromNative(card.Node), checked((int)card.Cut))),
      output.OnTimeBefore,
      output.OnTimeAfter,
      output.P50Days,
      output.P85Days);
  }

  private static NativeGraphScopeOptions ToNativeOptions(GraphScopeOptions<Guid> options) =>
    new(
      ToNative(options.Milestone),
      options.Seed,
      options.TargetHours,
      options.Goal,
      checked((uint)options.PersonCount),
      checked((uint)options.RunCount),
      checked((uint)options.ScreenRunCount),
      checked((uint)options.ThreadCount),
      checked((uint)options.EvaluationLimit),
      checked((uint)options.TimeLimit.TotalMilliseconds));

  private sealed record ScopeInput(
    IReadOnlyCollection<Guid> Nodes,
    IReadOnlyCollection<GraphEdge<Guid>> Edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> WorkItems,
    IReadOnlyCollection<GraphAssignee<Guid>> Assignees,
    IReadOnlyCollection<Guid> Cards,
    GraphScopeOptions<Guid> Options);
}
