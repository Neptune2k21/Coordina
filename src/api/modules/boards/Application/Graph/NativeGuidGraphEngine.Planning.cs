namespace Coordina.Api.Modules.Boards.Application.Graph;

// The calls that plan a board's milestones: their forecast, what to cut so one
// ships on time, and who should take which card.
public sealed partial class NativeGuidGraphEngine
{
  private delegate NativeGraphStatus PlanCall(ref NativeGraphBoard board, IntPtr[] buffers);

  private delegate NativeGraphStatus ScopeCall(
    ref NativeGraphBoard board,
    NativeGraphNodeId[] cards,
    UIntPtr cardCount,
    ref NativeGraphScopeOptions options,
    ref NativeGraphScopeCut output);

  private delegate NativeGraphStatus StaffingCall(
    ref NativeGraphBoard board,
    ref NativeGraphStaffingOptions options,
    ref NativeGraphStaffing output);

  public GraphForecast<Guid> Forecast(GraphBoard<Guid> board, GraphSimulation simulation)
  {
    ArgumentNullException.ThrowIfNull(board);

    var native = ToNative(simulation);
    var milestones = new NativeGraphMilestoneForecast[board.Milestones.Count];
    var cards = new NativeGraphCardForecast[board.Milestones.Count * board.Nodes.Count];
    var plan = new NativeGraphPlannedCard[board.Nodes.Count];
    var output = new NativeGraphForecastOutput
    {
      MilestoneCapacity = ToNativeCount(milestones.Length),
      CardCapacity = ToNativeCount(cards.Length),
      PlanCapacity = ToNativeCount(plan.Length)
    };

    Run(
      "forecast",
      board,
      [milestones, cards, plan],
      (ref NativeGraphBoard input, IntPtr[] buffers) =>
      {
        output.Milestones = buffers[0];
        output.Cards = buffers[1];
        output.Plan = buffers[2];
        return NativeMethods.Forecast(ref input, ref native, ref output);
      });

    var cardsByMilestone = Written(cards, output.CardCount, card => card)
      .ToLookup(
        card => card.Milestone,
        card => new GraphCardForecast<Guid>(FromNative(card.Node), card.Criticality, card.Sensitivity));

    return new GraphForecast<Guid>(
      Written(milestones, output.MilestoneCount, milestone => milestone)
        .Select((milestone, index) => FromNative(milestone, cardsByMilestone[(uint)index]))
        .ToArray(),
      Written(
        plan,
        output.PlanCount,
        card => new GraphPlannedCard<Guid>(
          FromNative(card.Node),
          card.Person < board.PersonCount ? (int)card.Person : null,
          card.StartDays,
          card.FinishDays)));
  }

  public GraphScopeCut<Guid> ProposeScope(
    GraphBoard<Guid> board,
    IReadOnlyCollection<Guid> keep,
    GraphScopeOptions<Guid> options) =>
    RunScope("propose_scope", board, keep, options, NativeMethods.ProposeScope);

  public GraphScopeCut<Guid> EvaluateScope(
    GraphBoard<Guid> board,
    IReadOnlyCollection<Guid> cuts,
    GraphScopeOptions<Guid> options) =>
    RunScope("evaluate_scope", board, cuts, options, NativeMethods.EvaluateScope);

  public GraphStaffing<Guid> ProposeStaffing(
    GraphBoard<Guid> board,
    IReadOnlyCollection<int> receivers,
    IReadOnlyCollection<GraphAssignee<Guid>> skilled,
    GraphStaffingOptions<Guid> options)
  {
    var nativeReceivers = ToNativePeople(receivers);
    var nativeSkilled = ToNativeAssignees(skilled);

    return RunStaffing(
      "propose_staffing",
      board,
      options,
      (ref NativeGraphBoard input, ref NativeGraphStaffingOptions native, ref NativeGraphStaffing output) =>
        NativeMethods.ProposeStaffing(
          ref input,
          nativeReceivers,
          ToNativeCount(nativeReceivers.Length),
          nativeSkilled,
          ToNativeCount(nativeSkilled.Length),
          ref native,
          ref output));
  }

  public GraphStaffing<Guid> EvaluateStaffing(
    GraphBoard<Guid> board,
    IReadOnlyCollection<GraphAssignee<Guid>> changes,
    GraphStaffingOptions<Guid> options)
  {
    var nativeChanges = ToNativeAssignees(changes);

    return RunStaffing(
      "evaluate_staffing",
      board,
      options,
      (ref NativeGraphBoard input, ref NativeGraphStaffingOptions native, ref NativeGraphStaffing output) =>
        NativeMethods.EvaluateStaffing(
          ref input,
          nativeChanges,
          ToNativeCount(nativeChanges.Length),
          ref native,
          ref output));
  }

  private static GraphScopeCut<Guid> RunScope(
    string operation,
    GraphBoard<Guid> board,
    IReadOnlyCollection<Guid> chosen,
    GraphScopeOptions<Guid> options,
    ScopeCall call)
  {
    ArgumentNullException.ThrowIfNull(board);
    ArgumentNullException.ThrowIfNull(chosen);
    ArgumentNullException.ThrowIfNull(options);

    var nativeCards = ToNativeNodes(chosen);
    var native = new NativeGraphScopeOptions(
      ToNative(options.Milestone),
      ToNative(options.Simulation),
      options.Goal,
      checked((uint)options.ScreenRunCount),
      checked((uint)options.EvaluationLimit),
      checked((uint)options.TimeLimit.TotalMilliseconds),
      0U);
    // A card that leaves the scope is a node, so the node count always fits.
    var cuts = new NativeGraphNodeId[board.Nodes.Count];
    var dropped = new NativeGraphDroppedCard[board.Nodes.Count];
    var output = new NativeGraphScopeCut
    {
      CutCapacity = ToNativeCount(cuts.Length),
      DroppedCapacity = ToNativeCount(dropped.Length)
    };

    Run(
      operation,
      board,
      [cuts, dropped],
      (ref NativeGraphBoard input, IntPtr[] buffers) =>
      {
        output.Cuts = buffers[0];
        output.Dropped = buffers[1];
        return call(ref input, nativeCards, ToNativeCount(nativeCards.Length), ref native, ref output);
      });

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

  private static GraphStaffing<Guid> RunStaffing(
    string operation,
    GraphBoard<Guid> board,
    GraphStaffingOptions<Guid> options,
    StaffingCall call)
  {
    ArgumentNullException.ThrowIfNull(board);
    ArgumentNullException.ThrowIfNull(options);

    var native = new NativeGraphStaffingOptions(
      ToNative(options.Milestone),
      ToNative(options.Simulation),
      checked((uint)options.ScreenRunCount),
      options.AllowReassignment ? 1U : 0U,
      checked((uint)options.EvaluationLimit),
      checked((uint)options.TimeLimit.TotalMilliseconds));
    // A change names a card, so the node count always fits.
    var changes = new NativeGraphStaffChange[board.Nodes.Count];
    var output = new NativeGraphStaffing { ChangeCapacity = ToNativeCount(changes.Length) };

    Run(
      operation,
      board,
      [changes],
      (ref NativeGraphBoard input, IntPtr[] buffers) =>
      {
        output.Changes = buffers[0];
        return call(ref input, ref native, ref output);
      });

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
      output.P85DaysAfter,
      output.OthersLateDaysBefore,
      output.OthersLateDaysAfter);
  }

  private static GraphMilestoneForecast<Guid> FromNative(
    NativeGraphMilestoneForecast milestone,
    IEnumerable<GraphCardForecast<Guid>> cards) =>
    new(
      FromNative(milestone.Node),
      milestone.P10Days,
      milestone.P50Days,
      milestone.P85Days,
      milestone.P95Days,
      milestone.OnTime,
      milestone.LateDays,
      milestone.ChainDays,
      checked((int)milestone.CardCount),
      checked((int)milestone.UnsizedCount),
      cards.ToArray());
}
