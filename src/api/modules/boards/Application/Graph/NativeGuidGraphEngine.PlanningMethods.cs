using System.Runtime.InteropServices;

namespace Coordina.Api.Modules.Boards.Application.Graph;

// The planning functions the C ABI exports, as declared in coordina_graph_engine.h.
public sealed partial class NativeGuidGraphEngine
{
  private static partial class NativeMethods
  {
    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_forecast",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus Forecast(
      ref NativeGraphBoard board,
      ref NativeGraphSimulation simulation,
      ref NativeGraphForecastOutput output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_propose_scope",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus ProposeScope(
      ref NativeGraphBoard board,
      NativeGraphNodeId[] keep,
      UIntPtr keepCount,
      ref NativeGraphScopeOptions options,
      ref NativeGraphScopeCut output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_evaluate_scope",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus EvaluateScope(
      ref NativeGraphBoard board,
      NativeGraphNodeId[] cuts,
      UIntPtr cutCount,
      ref NativeGraphScopeOptions options,
      ref NativeGraphScopeCut output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_propose_staffing",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus ProposeStaffing(
      ref NativeGraphBoard board,
      uint[] receivers,
      UIntPtr receiverCount,
      NativeGraphAssignee[] skilled,
      UIntPtr skilledCount,
      ref NativeGraphStaffingOptions options,
      ref NativeGraphStaffing output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_evaluate_staffing",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus EvaluateStaffing(
      ref NativeGraphBoard board,
      NativeGraphAssignee[] changes,
      UIntPtr changeCount,
      ref NativeGraphStaffingOptions options,
      ref NativeGraphStaffing output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_propose_actions",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus ProposeActions(
      ref NativeGraphBoard board,
      uint[] receivers,
      UIntPtr receiverCount,
      ref NativeGraphActionOptions options,
      ref NativeGraphActionOutput output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_rank_questions",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus RankQuestions(
      ref NativeGraphBoard board,
      NativeGraphCardToken[] tokens,
      UIntPtr tokenCount,
      NativeGraphDependencyHint[] hints,
      UIntPtr hintCount,
      ref NativeGraphQuestionOptions options,
      ref NativeGraphQuestionOutput output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_suggest_dependencies",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus SuggestDependencies(
      ref NativeGraphBoard board,
      NativeGraphCardToken[] tokens,
      UIntPtr tokenCount,
      ref NativeGraphNodeId source,
      uint limit,
      ref NativeGraphHintOutput output);

    [DllImport(
      LibraryName,
      EntryPoint = "coordina_graph_calibrate",
      CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeGraphStatus Calibrate(
      ref NativeGraphBoard board,
      ref NativeGraphCalibration output);
  }
}
