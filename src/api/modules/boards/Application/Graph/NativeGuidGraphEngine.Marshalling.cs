using System.Runtime.InteropServices;

namespace Coordina.Api.Modules.Boards.Application.Graph;

// The ABI structs, their conversions and the buffers native calls write to.
public sealed partial class NativeGuidGraphEngine
{
  private delegate UIntPtr NativeRead<TNative>(TNative[] output, UIntPtr capacity);

  private static Guid[] ReadNodes(int capacity, NativeRead<NativeGraphNodeId> read) =>
    Read(capacity, read, FromNative);

  private static GraphNodeAnalysis<Guid>[] ReadNodeAnalyses(
    int capacity,
    NativeRead<NativeGraphNodeAnalysis> read) =>
    Read(capacity, read, FromNative);

  private static GraphWorkRecommendation<Guid>[] ReadWorkRecommendations(
    int capacity,
    NativeRead<NativeGraphWorkRecommendation> read) =>
    Read(capacity, read, FromNative);

  private static T[] Read<TNative, T>(int capacity, NativeRead<TNative> read, Func<TNative, T> convert)
  {
    var output = new TNative[capacity];

    return Written(output, read(output, ToNativeCount(capacity)), convert);
  }

  private static T[] Written<TNative, T>(TNative[] buffer, UIntPtr count, Func<TNative, T> convert)
  {
    var written = checked((int)count.ToUInt64());

    if (written > buffer.Length)
    {
      throw new GraphEngineException(
        $"Native graph engine reported {written} items for a buffer of {buffer.Length}.");
    }

    return buffer.Take(written).Select(convert).ToArray();
  }

  // Pins the buffers a native call writes to through the pointers of its
  // output struct, for as long as the call runs.
  private static TResult WithPinned<TResult>(Array[] buffers, Func<IntPtr[], TResult> call)
  {
    var handles = new GCHandle[buffers.Length];

    try
    {
      for (var index = 0; index < buffers.Length; index++)
      {
        handles[index] = GCHandle.Alloc(buffers[index], GCHandleType.Pinned);
      }

      return call(handles.Select(handle => handle.AddrOfPinnedObject()).ToArray());
    }
    finally
    {
      foreach (var handle in handles.Where(handle => handle.IsAllocated))
      {
        handle.Free();
      }
    }
  }

  private readonly record struct MarshalledGraph(
    NativeGraphNodeId[] Nodes,
    NativeGraphEdge[] Edges,
    NativeGraphWorkItem[] WorkItems)
  {
    public UIntPtr NodeCount => ToNativeCount(Nodes.Length);

    public UIntPtr EdgeCount => ToNativeCount(Edges.Length);

    public UIntPtr WorkItemCount => ToNativeCount(WorkItems.Length);

    public static MarshalledGraph From(
      IReadOnlyCollection<Guid> nodes,
      IReadOnlyCollection<GraphEdge<Guid>> edges) =>
      From(nodes, edges, []);

    public static MarshalledGraph From(
      IReadOnlyCollection<Guid> nodes,
      IReadOnlyCollection<GraphEdge<Guid>> edges,
      IReadOnlyCollection<GraphWorkItem<Guid>> workItems)
    {
      ArgumentNullException.ThrowIfNull(nodes);
      ArgumentNullException.ThrowIfNull(edges);
      ArgumentNullException.ThrowIfNull(workItems);

      return new MarshalledGraph(ToNativeNodes(nodes), ToNativeEdges(edges), ToNativeWorkItems(workItems));
    }
  }

  private static GraphNodeAnalysis<Guid> FromNative(NativeGraphNodeAnalysis analysis) =>
    new(
      FromNative(analysis.Node),
      checked((int)analysis.DependencyCount),
      checked((int)analysis.DependentCount),
      checked((int)analysis.DependencyDepth),
      checked((int)analysis.DependentDepth),
      checked((int)analysis.TransitiveDependentCount));

  private static GraphWorkRecommendation<Guid> FromNative(
    NativeGraphWorkRecommendation recommendation) =>
    new(
      FromNative(recommendation.Node),
      checked((int)recommendation.Score),
      checked((int)recommendation.BlockerCount),
      checked((int)recommendation.UnlockScore),
      checked((int)recommendation.UrgencyScore),
      checked((int)recommendation.PriorityScore),
      checked((int)recommendation.ProgressScore),
      recommendation.IsCriticalPath != 0U,
      recommendation.EffortScore,
      recommendation.AssignmentScore,
      checked((int)recommendation.MomentumScore),
      checked((int)recommendation.ContextScore),
      recommendation.FitsMaxSize != 0U,
      recommendation.IsAssignmentMatch != 0U,
      recommendation.IsContextMatch != 0U,
      recommendation.IsInProgress != 0U);

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

  private static NativeGraphWorkItem[] ToNativeWorkItems(
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems) =>
    workItems
      .Select(item => new NativeGraphWorkItem(
        ToNative(item.Node),
        item.IsCompleted ? 1U : 0U,
        checked((uint)item.Priority),
        item.DueDays,
        checked((uint)item.SubtaskTotal),
        checked((uint)item.SubtaskCompleted),
        checked((uint)item.Position),
        (uint)item.Size,
        (uint)item.Assignment,
        item.IsInProgress ? 1U : 0U,
        item.IsContextMatch ? 1U : 0U))
      .ToArray();

  private static NativeGraphAssignee[] ToNativeAssignees(
    IReadOnlyCollection<GraphAssignee<Guid>> assignees) =>
    assignees
      .Select(assignee => new NativeGraphAssignee(ToNative(assignee.Node), checked((uint)assignee.Person), 0U))
      .ToArray();

  private static NativeGraphPlanningOptions ToNativeOptions(
    GraphPlanningOptions options) =>
    new(
      (uint)options.MaxSize,
      options.PreferAssigned ? 1U : 0U,
      options.IncludeUnassigned ? 1U : 0U);

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

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphNodeAnalysis(
    NativeGraphNodeId Node,
    uint DependencyCount,
    uint DependentCount,
    uint DependencyDepth,
    uint DependentDepth,
    uint TransitiveDependentCount);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphWorkItem(
    NativeGraphNodeId Node,
    uint IsCompleted,
    uint Priority,
    int DueDays,
    uint SubtaskTotal,
    uint SubtaskCompleted,
    uint Position,
    uint Size,
    uint Assignment,
    uint IsInProgress,
    uint IsContextMatch);

  [StructLayout(LayoutKind.Sequential)]
  private record struct NativeGraphPlanningOptions(
    uint MaxSize,
    uint PreferAssigned,
    uint IncludeUnassigned);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphWorkRecommendation(
    NativeGraphNodeId Node,
    uint Score,
    uint BlockerCount,
    uint UnlockScore,
    uint UrgencyScore,
    uint PriorityScore,
    uint ProgressScore,
    uint IsCriticalPath,
    int EffortScore,
    int AssignmentScore,
    uint MomentumScore,
    uint ContextScore,
    uint FitsMaxSize,
    uint IsAssignmentMatch,
    uint IsContextMatch,
    uint IsInProgress);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphAssignee(
    NativeGraphNodeId Node,
    uint Person,
    uint Reserved);

  [StructLayout(LayoutKind.Sequential)]
  private record struct NativeGraphForecastOptions(
    NativeGraphNodeId Milestone,
    ulong Seed,
    uint PersonCount,
    uint RunCount,
    uint ThreadCount,
    uint Reserved);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphCardForecast(
    NativeGraphNodeId Node,
    double Criticality,
    double Sensitivity);

  [StructLayout(LayoutKind.Sequential)]
  private record struct NativeGraphScopeOptions(
    NativeGraphNodeId Milestone,
    ulong Seed,
    double TargetHours,
    double Goal,
    uint PersonCount,
    uint RunCount,
    uint ScreenRunCount,
    uint ThreadCount,
    uint EvaluationLimit,
    uint TimeLimitMs);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphDroppedCard(
    NativeGraphNodeId Node,
    uint Cut,
    uint Reserved);

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphScopeCut
  {
    public IntPtr Cuts;
    public UIntPtr CutCapacity;
    public UIntPtr CutCount;
    public IntPtr Dropped;
    public UIntPtr DroppedCapacity;
    public UIntPtr DroppedCount;
    public double OnTimeBefore;
    public double OnTimeAfter;
    public double P50Days;
    public double P85Days;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphMilestoneForecast
  {
    public IntPtr Cards;
    public UIntPtr CardCapacity;
    public UIntPtr CardCount;
    public double P50Days;
    public double P85Days;
    public uint UnsizedCount;
    public uint Reserved;
  }

  private enum NativeGraphStatus
  {
    Ok = 0,
    InvalidArgument = 1,
    InsufficientCapacity = 2,
    Cycle = 3
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphNodeBuffer
  {
    public IntPtr Items;
    public UIntPtr Capacity;
    public UIntPtr Count;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphBoardAnalysis
  {
    public NativeGraphNodeBuffer ReadyNodes;
    public NativeGraphNodeBuffer DependencyOrder;
    public NativeGraphNodeBuffer CriticalPath;
    public IntPtr NodeAnalyses;
    public UIntPtr NodeAnalysisCapacity;
    public UIntPtr NodeAnalysisCount;
    public IntPtr Recommendations;
    public UIntPtr RecommendationCapacity;
    public UIntPtr RecommendationCount;
    public uint IsAcyclic;
    public uint Reserved;
  }

  private delegate NativeGraphStatus BoardAnalysisCall(ref NativeGraphBoardAnalysis output);

  private sealed class BoardAnalysisBuffers(int nodeCount, int workItemCount)
  {
    private readonly NativeGraphNodeId[] _readyNodes = new NativeGraphNodeId[nodeCount];
    private readonly NativeGraphNodeId[] _dependencyOrder = new NativeGraphNodeId[nodeCount];
    private readonly NativeGraphNodeId[] _criticalPath = new NativeGraphNodeId[nodeCount];
    private readonly NativeGraphNodeAnalysis[] _nodeAnalyses = new NativeGraphNodeAnalysis[nodeCount];
    private readonly NativeGraphWorkRecommendation[] _recommendations =
      new NativeGraphWorkRecommendation[workItemCount];
    private NativeGraphBoardAnalysis _output;

    public NativeGraphStatus Run(BoardAnalysisCall call)
    {
      var handles = new[]
      {
        GCHandle.Alloc(_readyNodes, GCHandleType.Pinned),
        GCHandle.Alloc(_dependencyOrder, GCHandleType.Pinned),
        GCHandle.Alloc(_criticalPath, GCHandleType.Pinned),
        GCHandle.Alloc(_nodeAnalyses, GCHandleType.Pinned),
        GCHandle.Alloc(_recommendations, GCHandleType.Pinned)
      };

      try
      {
        _output = new NativeGraphBoardAnalysis
        {
          ReadyNodes = Buffer(handles[0], _readyNodes.Length),
          DependencyOrder = Buffer(handles[1], _dependencyOrder.Length),
          CriticalPath = Buffer(handles[2], _criticalPath.Length),
          NodeAnalyses = handles[3].AddrOfPinnedObject(),
          NodeAnalysisCapacity = ToNativeCount(_nodeAnalyses.Length),
          Recommendations = handles[4].AddrOfPinnedObject(),
          RecommendationCapacity = ToNativeCount(_recommendations.Length)
        };

        return call(ref _output);
      }
      finally
      {
        foreach (var handle in handles)
        {
          handle.Free();
        }
      }
    }

    public GraphBoardAnalysis<Guid> ToAnalysis() =>
      new(
        _output.IsAcyclic != 0U,
        Written(_readyNodes, _output.ReadyNodes.Count, FromNative),
        Written(_dependencyOrder, _output.DependencyOrder.Count, FromNative),
        Written(_criticalPath, _output.CriticalPath.Count, FromNative),
        Written(_nodeAnalyses, _output.NodeAnalysisCount, FromNative),
        Written(_recommendations, _output.RecommendationCount, FromNative));

    private static NativeGraphNodeBuffer Buffer(GCHandle handle, int capacity) =>
      new()
      {
        Items = handle.AddrOfPinnedObject(),
        Capacity = ToNativeCount(capacity)
      };
  }
}
