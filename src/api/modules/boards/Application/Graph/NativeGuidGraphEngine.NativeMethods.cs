using System.Runtime.InteropServices;

namespace Coordina.Api.Modules.Boards.Application.Graph;

// The functions the C ABI exports, as declared in coordina_graph_engine.h.
public sealed partial class NativeGuidGraphEngine
{
  private const string LibraryName = "coordina_graph_engine";

  private static partial class NativeMethods
  {
    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_evaluate_edge",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern GraphEdgeStatus EvaluateEdge(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphNodeId source,
      NativeGraphNodeId target);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_suggest_targets",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr SuggestTargets(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphNodeId source,
      [Out]
      NativeGraphNodeId[] output,
      UIntPtr outputCapacity);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_is_acyclic",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern int IsAcyclic(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_topological_sort",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr TopologicalSort(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      [Out]
      NativeGraphNodeId[] output,
      UIntPtr outputCapacity);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_ready_nodes",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr ReadyNodes(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      [Out]
      NativeGraphNodeId[] output,
      UIntPtr outputCapacity);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_impacted_dependents",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr ImpactedDependents(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphNodeId dependency,
      [Out]
      NativeGraphNodeId[] output,
      UIntPtr outputCapacity);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_analyze_nodes",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr AnalyzeNodes(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      [Out]
      NativeGraphNodeAnalysis[] output,
      UIntPtr outputCapacity);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_critical_path",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr CriticalPath(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphWorkItem[] workItems,
      UIntPtr workItemCount,
      [Out]
      NativeGraphNodeId[] output,
      UIntPtr outputCapacity);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_plan_work",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr PlanWork(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphWorkItem[] workItems,
      UIntPtr workItemCount,
      ref NativeGraphPlanningOptions options,
      [Out]
      NativeGraphWorkRecommendation[] output,
      UIntPtr outputCapacity);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_analyze_board",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus AnalyzeBoard(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphWorkItem[] workItems,
      UIntPtr workItemCount,
      ref NativeGraphPlanningOptions options,
      ref NativeGraphBoardAnalysis output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_forecast",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus Forecast(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphWorkItem[] workItems,
      UIntPtr workItemCount,
      NativeGraphAssignee[] assignees,
      UIntPtr assigneeCount,
      ref NativeGraphForecastOptions options,
      ref NativeGraphMilestoneForecast output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_propose_scope",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus ProposeScope(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphWorkItem[] workItems,
      UIntPtr workItemCount,
      NativeGraphAssignee[] assignees,
      UIntPtr assigneeCount,
      NativeGraphNodeId[] keep,
      UIntPtr keepCount,
      ref NativeGraphScopeOptions options,
      ref NativeGraphScopeCut output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_evaluate_scope",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus EvaluateScope(
      NativeGraphNodeId[] nodes,
      UIntPtr nodeCount,
      NativeGraphEdge[] edges,
      UIntPtr edgeCount,
      NativeGraphWorkItem[] workItems,
      UIntPtr workItemCount,
      NativeGraphAssignee[] assignees,
      UIntPtr assigneeCount,
      NativeGraphNodeId[] cuts,
      UIntPtr cutCount,
      ref NativeGraphScopeOptions options,
      ref NativeGraphScopeCut output);
  }
}
