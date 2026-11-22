using System.Runtime.InteropServices;

namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>
/// Runs the C++ graph engine through its C ABI. Split into partial files: this
/// one holds the calls, the Marshalling file the ABI structs and their
/// conversions, and the NativeMethods file the imported functions.
/// </summary>
public sealed partial class NativeGuidGraphEngine : IGraphEngine<Guid>
{
  public GraphEdgeEvaluation EvaluateEdge(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    Guid source,
    Guid target)
  {
    var graph = MarshalledGraph.From(nodes, edges);
    var status = NativeMethods.EvaluateEdge(
      graph.Nodes,
      graph.NodeCount,
      graph.Edges,
      graph.EdgeCount,
      ToNative(source),
      ToNative(target));

    return new GraphEdgeEvaluation(status);
  }

  public IReadOnlyCollection<Guid> SuggestTargets(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    Guid source,
    int maxSuggestions)
  {
    var graph = MarshalledGraph.From(nodes, edges);

    if (maxSuggestions < 1)
    {
      throw new ArgumentOutOfRangeException(
        nameof(maxSuggestions),
        "At least one suggestion is required.");
    }

    return ReadNodes(
      maxSuggestions,
      (output, capacity) => NativeMethods.SuggestTargets(
        graph.Nodes,
        graph.NodeCount,
        graph.Edges,
        graph.EdgeCount,
        ToNative(source),
        output,
        capacity));
  }

  public bool IsAcyclic(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges)
  {
    var graph = MarshalledGraph.From(nodes, edges);

    return NativeMethods.IsAcyclic(graph.Nodes, graph.NodeCount, graph.Edges, graph.EdgeCount) == 1;
  }

  public IReadOnlyCollection<Guid> SortTopologically(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges)
  {
    var graph = MarshalledGraph.From(nodes, edges);

    return ReadNodes(
      graph.Nodes.Length,
      (output, capacity) => NativeMethods.TopologicalSort(
        graph.Nodes,
        graph.NodeCount,
        graph.Edges,
        graph.EdgeCount,
        output,
        capacity));
  }

  public IReadOnlyCollection<Guid> FindReadyNodes(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges)
  {
    var graph = MarshalledGraph.From(nodes, edges);

    return ReadNodes(
      graph.Nodes.Length,
      (output, capacity) => NativeMethods.ReadyNodes(
        graph.Nodes,
        graph.NodeCount,
        graph.Edges,
        graph.EdgeCount,
        output,
        capacity));
  }

  public IReadOnlyCollection<Guid> FindImpactedDependents(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    Guid dependency)
  {
    var graph = MarshalledGraph.From(nodes, edges);

    return ReadNodes(
      graph.Nodes.Length,
      (output, capacity) => NativeMethods.ImpactedDependents(
        graph.Nodes,
        graph.NodeCount,
        graph.Edges,
        graph.EdgeCount,
        ToNative(dependency),
        output,
        capacity));
  }

  public IReadOnlyCollection<GraphNodeAnalysis<Guid>> AnalyzeNodes(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges)
  {
    var graph = MarshalledGraph.From(nodes, edges);

    return ReadNodeAnalyses(
      graph.Nodes.Length,
      (output, capacity) => NativeMethods.AnalyzeNodes(
        graph.Nodes,
        graph.NodeCount,
        graph.Edges,
        graph.EdgeCount,
        output,
        capacity));
  }

  public IReadOnlyCollection<Guid> FindCriticalPath(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems)
  {
    var graph = MarshalledGraph.From(nodes, edges, workItems);

    return ReadNodes(
      graph.Nodes.Length,
      (output, capacity) => NativeMethods.CriticalPath(
        graph.Nodes,
        graph.NodeCount,
        graph.Edges,
        graph.EdgeCount,
        graph.WorkItems,
        graph.WorkItemCount,
        output,
        capacity));
  }

  public IReadOnlyCollection<GraphWorkRecommendation<Guid>> PlanWork(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    GraphPlanningOptions? options = null)
  {
    var graph = MarshalledGraph.From(nodes, edges, workItems);
    var nativeOptions = ToNativeOptions(options ?? GraphPlanningOptions.Default);

    return ReadWorkRecommendations(
      graph.WorkItems.Length,
      (output, capacity) => NativeMethods.PlanWork(
        graph.Nodes,
        graph.NodeCount,
        graph.Edges,
        graph.EdgeCount,
        graph.WorkItems,
        graph.WorkItemCount,
        ref nativeOptions,
        output,
        capacity));
  }

  public GraphBoardAnalysis<Guid> AnalyzeBoard(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    GraphPlanningOptions? options = null)
  {
    var graph = MarshalledGraph.From(nodes, edges, workItems);
    var nativeOptions = ToNativeOptions(options ?? GraphPlanningOptions.Default);
    var buffers = new BoardAnalysisBuffers(graph.Nodes.Length, graph.WorkItems.Length);

    var status = buffers.Run((ref NativeGraphBoardAnalysis output) => NativeMethods.AnalyzeBoard(
      graph.Nodes,
      graph.NodeCount,
      graph.Edges,
      graph.EdgeCount,
      graph.WorkItems,
      graph.WorkItemCount,
      ref nativeOptions,
      ref output));

    if (status != NativeGraphStatus.Ok)
    {
      throw new GraphEngineException("analyze_board", (int)status);
    }

    return buffers.ToAnalysis();
  }

  public GraphMilestoneForecast<Guid> Forecast(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    IReadOnlyCollection<GraphAssignee<Guid>> assignees,
    GraphForecastOptions<Guid> options)
  {
    var graph = MarshalledGraph.From(nodes, edges, workItems);
    ArgumentNullException.ThrowIfNull(assignees);
    ArgumentNullException.ThrowIfNull(options);

    var nativeAssignees = ToNativeAssignees(assignees);
    var nativeOptions = new NativeGraphForecastOptions(
      ToNative(options.Milestone),
      options.Seed,
      checked((uint)options.PersonCount),
      checked((uint)options.RunCount),
      checked((uint)options.ThreadCount),
      0U);
    // Every open card of the scope is a node, so the node count always fits.
    var cards = new NativeGraphCardForecast[graph.Nodes.Length];
    var output = new NativeGraphMilestoneForecast { CardCapacity = ToNativeCount(cards.Length) };

    var status = WithPinned(
      [cards],
      pointers =>
      {
        output.Cards = pointers[0];
        return NativeMethods.Forecast(
          graph.Nodes,
          graph.NodeCount,
          graph.Edges,
          graph.EdgeCount,
          graph.WorkItems,
          graph.WorkItemCount,
          nativeAssignees,
          ToNativeCount(nativeAssignees.Length),
          ref nativeOptions,
          ref output);
      });

    if (status != NativeGraphStatus.Ok)
    {
      throw new GraphEngineException("forecast", (int)status);
    }

    return new GraphMilestoneForecast<Guid>(
      output.P50Days,
      output.P85Days,
      checked((int)output.UnsizedCount),
      Written(
        cards,
        output.CardCount,
        card => new GraphCardForecast<Guid>(FromNative(card.Node), card.Criticality, card.Sensitivity)));
  }
}
