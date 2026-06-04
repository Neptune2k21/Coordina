using System.Runtime.InteropServices;

namespace Coordina.Api.Modules.Tasks.Application.Graph;

public sealed class NativeGuidGraphEngine : IGraphEngine<Guid>
{
  private const string LibraryName = "coordina_graph_engine";

  public GraphEdgeEvaluation EvaluateEdge(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    Guid source,
    Guid target)
  {
    ArgumentNullException.ThrowIfNull(nodes);
    ArgumentNullException.ThrowIfNull(edges);

    var nativeNodes = ToNativeNodes(nodes);
    var nativeEdges = ToNativeEdges(edges);
    var status = NativeMethods.EvaluateEdge(
      nativeNodes,
      ToNativeCount(nativeNodes.Length),
      nativeEdges,
      ToNativeCount(nativeEdges.Length),
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
    ArgumentNullException.ThrowIfNull(nodes);
    ArgumentNullException.ThrowIfNull(edges);

    if (maxSuggestions < 1)
    {
      throw new ArgumentOutOfRangeException(
        nameof(maxSuggestions),
        "At least one suggestion is required.");
    }

    var nativeNodes = ToNativeNodes(nodes);
    var nativeEdges = ToNativeEdges(edges);
    var output = new NativeGraphNodeId[maxSuggestions];
    var count = NativeMethods.SuggestTargets(
      nativeNodes,
      ToNativeCount(nativeNodes.Length),
      nativeEdges,
      ToNativeCount(nativeEdges.Length),
      ToNative(source),
      output,
      ToNativeCount(output.Length));

    return output
      .Take(checked((int)count))
      .Select(FromNative)
      .ToArray();
  }

  public bool IsAcyclic(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges)
  {
    ArgumentNullException.ThrowIfNull(nodes);
    ArgumentNullException.ThrowIfNull(edges);

    var nativeNodes = ToNativeNodes(nodes);
    var nativeEdges = ToNativeEdges(edges);
    var result = NativeMethods.IsAcyclic(
      nativeNodes,
      ToNativeCount(nativeNodes.Length),
      nativeEdges,
      ToNativeCount(nativeEdges.Length));

    return result == 1;
  }

  public IReadOnlyCollection<Guid> SortTopologically(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges)
  {
    ArgumentNullException.ThrowIfNull(nodes);
    ArgumentNullException.ThrowIfNull(edges);

    var nativeNodes = ToNativeNodes(nodes);
    var nativeEdges = ToNativeEdges(edges);

    return ReadNodeCollection(
      nativeNodes.Length,
      output => NativeMethods.TopologicalSort(
        nativeNodes,
        ToNativeCount(nativeNodes.Length),
        nativeEdges,
        ToNativeCount(nativeEdges.Length),
        output,
        ToNativeCount(output.Length)));
  }

  public IReadOnlyCollection<Guid> FindReadyNodes(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges)
  {
    ArgumentNullException.ThrowIfNull(nodes);
    ArgumentNullException.ThrowIfNull(edges);

    var nativeNodes = ToNativeNodes(nodes);
    var nativeEdges = ToNativeEdges(edges);

    return ReadNodeCollection(
      nativeNodes.Length,
      output => NativeMethods.ReadyNodes(
        nativeNodes,
        ToNativeCount(nativeNodes.Length),
        nativeEdges,
        ToNativeCount(nativeEdges.Length),
        output,
        ToNativeCount(output.Length)));
  }

  public IReadOnlyCollection<Guid> FindImpactedDependents(
    IReadOnlyCollection<Guid> nodes,
    IReadOnlyCollection<GraphEdge<Guid>> edges,
    Guid dependency)
  {
    ArgumentNullException.ThrowIfNull(nodes);
    ArgumentNullException.ThrowIfNull(edges);

    var nativeNodes = ToNativeNodes(nodes);
    var nativeEdges = ToNativeEdges(edges);

    return ReadNodeCollection(
      nativeNodes.Length,
      output => NativeMethods.ImpactedDependents(
        nativeNodes,
        ToNativeCount(nativeNodes.Length),
        nativeEdges,
        ToNativeCount(nativeEdges.Length),
        ToNative(dependency),
        output,
        ToNativeCount(output.Length)));
  }

  private static Guid[] ReadNodeCollection(
    int capacity,
    Func<NativeGraphNodeId[], UIntPtr> read)
  {
    var output = new NativeGraphNodeId[capacity];
    var count = read(output);

    return output
      .Take(checked((int)count))
      .Select(FromNative)
      .ToArray();
  }

  private static NativeGraphNodeId[] ToNativeNodes(
    IReadOnlyCollection<Guid> nodes) =>
    nodes.Select(ToNative).ToArray();

  private static NativeGraphEdge[] ToNativeEdges(
    IReadOnlyCollection<GraphEdge<Guid>> edges) =>
    edges
      .Select(edge => new NativeGraphEdge(
        ToNative(edge.Source),
        ToNative(edge.Target)))
      .ToArray();

  private static NativeGraphNodeId ToNative(Guid value)
  {
    Span<byte> bytes = stackalloc byte[16];
    value.TryWriteBytes(bytes);

    return new NativeGraphNodeId(
      BitConverter.ToUInt64(bytes[..8]),
      BitConverter.ToUInt64(bytes[8..]));
  }

  private static Guid FromNative(NativeGraphNodeId value)
  {
    Span<byte> bytes = stackalloc byte[16];
    BitConverter.TryWriteBytes(bytes[..8], value.High);
    BitConverter.TryWriteBytes(bytes[8..], value.Low);

    return new Guid(bytes);
  }

  private static UIntPtr ToNativeCount(int count) => new((ulong)count);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphNodeId(
    ulong High,
    ulong Low);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphEdge(
    NativeGraphNodeId Source,
    NativeGraphNodeId Target);

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
  }
}
