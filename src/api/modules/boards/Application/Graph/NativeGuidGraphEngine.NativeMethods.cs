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
      EntryPoint = "coordina_graph_last_error",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr LastError();
  }
}
