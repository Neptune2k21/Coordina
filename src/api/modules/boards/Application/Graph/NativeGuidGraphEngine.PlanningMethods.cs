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
  }
}
