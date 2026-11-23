namespace Coordina.Api.Modules.Boards.Application.Graph;

// Who should take which card so a milestone ships soonest.
public sealed partial class NativeGuidGraphEngine
{
  private delegate NativeGraphStatus StaffingCall(
    MarshalledGraph graph,
    NativeGraphAssignee[] assignees,
    ref NativeGraphStaffingOptions options,
    ref NativeGraphStaffing output);

  public GraphStaffing<Guid> ProposeStaffing(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    IReadOnlyCollection<GraphAssignee<Guid>> assignees,
    IReadOnlyCollection<int> receivers,
    IReadOnlyCollection<GraphAssignee<Guid>> skilled,
    GraphStaffingOptions<Guid> options)
  {
    ArgumentNullException.ThrowIfNull(receivers);
    ArgumentNullException.ThrowIfNull(skilled);

    var nativeReceivers = receivers.Select(person => checked((uint)person)).ToArray();
    var nativeSkilled = ToNativeAssignees(skilled);

    return RunStaffing(
      MarshalledGraph.From(nodes, edges, workItems),
      assignees,
      options,
      (MarshalledGraph graph, NativeGraphAssignee[] team, ref NativeGraphStaffingOptions native, ref NativeGraphStaffing output) =>
        NativeMethods.ProposeStaffing(
          graph.Nodes,
          graph.NodeCount,
          graph.Edges,
          graph.EdgeCount,
          graph.WorkItems,
          graph.WorkItemCount,
          team,
          ToNativeCount(team.Length),
          nativeReceivers,
          ToNativeCount(nativeReceivers.Length),
          nativeSkilled,
          ToNativeCount(nativeSkilled.Length),
          ref native,
          ref output),
      "propose_staffing");
  }

  public GraphStaffing<Guid> EvaluateStaffing(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    IReadOnlyCollection<GraphAssignee<Guid>> assignees,
    IReadOnlyCollection<GraphAssignee<Guid>> changes,
    GraphStaffingOptions<Guid> options)
  {
    ArgumentNullException.ThrowIfNull(changes);

    var nativeChanges = ToNativeAssignees(changes);

    return RunStaffing(
      MarshalledGraph.From(nodes, edges, workItems),
      assignees,
      options,
      (MarshalledGraph graph, NativeGraphAssignee[] team, ref NativeGraphStaffingOptions native, ref NativeGraphStaffing output) =>
        NativeMethods.EvaluateStaffing(
          graph.Nodes,
          graph.NodeCount,
          graph.Edges,
          graph.EdgeCount,
          graph.WorkItems,
          graph.WorkItemCount,
          team,
          ToNativeCount(team.Length),
          nativeChanges,
          ToNativeCount(nativeChanges.Length),
          ref native,
          ref output),
      "evaluate_staffing");
  }

  private static GraphStaffing<Guid> RunStaffing(
    MarshalledGraph graph,
    IReadOnlyCollection<GraphAssignee<Guid>> assignees,
    GraphStaffingOptions<Guid> options,
    StaffingCall call,
    string operation)
  {
    ArgumentNullException.ThrowIfNull(assignees);
    ArgumentNullException.ThrowIfNull(options);

    var team = ToNativeAssignees(assignees);
    var nativeOptions = new NativeGraphStaffingOptions(
      ToNative(options.Milestone),
      options.Seed,
      checked((uint)options.PersonCount),
      checked((uint)options.RunCount),
      checked((uint)options.ScreenRunCount),
      checked((uint)options.ThreadCount),
      options.AllowReassignment ? 1U : 0U,
      checked((uint)options.EvaluationLimit),
      checked((uint)options.TimeLimit.TotalMilliseconds),
      0U);
    // A change names a card, so the node count always fits.
    var changes = new NativeGraphStaffChange[graph.Nodes.Length];
    var output = new NativeGraphStaffing { ChangeCapacity = ToNativeCount(changes.Length) };

    var status = WithPinned(
      [changes],
      pointers =>
      {
        output.Changes = pointers[0];
        return call(graph, team, ref nativeOptions, ref output);
      });

    if (status != NativeGraphStatus.Ok)
    {
      throw new GraphEngineException(operation, (int)status);
    }

    return new GraphStaffing<Guid>(
      Written(
        changes,
        output.ChangeCount,
        change => new GraphStaffChange<Guid>(
          FromNative(change.Node),
          checked((int)change.Person),
          change.StartDaysBefore,
          change.StartDaysAfter)),
      output.P50DaysBefore,
      output.P85DaysBefore,
      output.P50DaysAfter,
      output.P85DaysAfter);
  }
}
