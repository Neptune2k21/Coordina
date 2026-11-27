using System.Runtime.InteropServices;

namespace Coordina.Api.Modules.Boards.Application.Graph;

// The ABI structs of the planning functions, in the header's order.
public sealed partial class NativeGuidGraphEngine
{
  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphAssignee(
    NativeGraphNodeId Node,
    uint Person,
    uint Reserved);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphCardLabel(
    NativeGraphNodeId Node,
    uint Label,
    uint Reserved);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphMilestone(
    NativeGraphNodeId Node,
    double TargetHours,
    double Weight);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphFinishedCard(
    NativeGraphNodeId Node,
    double Hours,
    double AgeDays);

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphBoard
  {
    public IntPtr Nodes;
    public UIntPtr NodeCount;
    public IntPtr Edges;
    public UIntPtr EdgeCount;
    public IntPtr WorkItems;
    public UIntPtr WorkItemCount;
    public IntPtr Assignees;
    public UIntPtr AssigneeCount;
    public IntPtr Labels;
    public UIntPtr LabelCount;
    public IntPtr Milestones;
    public UIntPtr MilestoneCount;
    public IntPtr History;
    public UIntPtr HistoryCount;
    public uint PersonCount;
    public uint DiscoveredCount;
    public uint FinishedCount;
    public uint ExtraPeople;
  }

  [StructLayout(LayoutKind.Sequential)]
  private record struct NativeGraphSimulation(
    ulong Seed,
    uint RunCount,
    uint ThreadCount);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphMilestoneForecast(
    NativeGraphNodeId Node,
    double P10Days,
    double P50Days,
    double P85Days,
    double P95Days,
    double OnTime,
    double LateDays,
    double ChainDays,
    uint CardCount,
    uint UnsizedCount);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphCardForecast(
    NativeGraphNodeId Node,
    uint Milestone,
    uint Reserved,
    double Criticality,
    double Sensitivity);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphPlannedCard(
    NativeGraphNodeId Node,
    uint Person,
    uint Reserved,
    double StartDays,
    double FinishDays);

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphForecastOutput
  {
    public IntPtr Milestones;
    public UIntPtr MilestoneCapacity;
    public UIntPtr MilestoneCount;
    public IntPtr Cards;
    public UIntPtr CardCapacity;
    public UIntPtr CardCount;
    public IntPtr Plan;
    public UIntPtr PlanCapacity;
    public UIntPtr PlanCount;
  }

  [StructLayout(LayoutKind.Sequential)]
  private record struct NativeGraphScopeOptions(
    NativeGraphNodeId Milestone,
    NativeGraphSimulation Simulation,
    double Goal,
    uint ScreenRunCount,
    uint EvaluationLimit,
    uint TimeLimitMs,
    uint Reserved);

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
  private record struct NativeGraphStaffingOptions(
    NativeGraphNodeId Milestone,
    NativeGraphSimulation Simulation,
    uint ScreenRunCount,
    uint AllowReassignment,
    uint EvaluationLimit,
    uint TimeLimitMs);

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphStaffChange(
    NativeGraphNodeId Node,
    uint Person,
    uint Reserved,
    double StartDaysBefore,
    double StartDaysAfter);

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphStaffing
  {
    public IntPtr Changes;
    public UIntPtr ChangeCapacity;
    public UIntPtr ChangeCount;
    public double P50DaysBefore;
    public double P85DaysBefore;
    public double P50DaysAfter;
    public double P85DaysAfter;
    public double OthersLateDaysBefore;
    public double OthersLateDaysAfter;
  }

  [StructLayout(LayoutKind.Sequential)]
  private readonly record struct NativeGraphDurationRatio(
    double Low,
    double Median,
    double High,
    uint Key,
    uint CardCount);

  [StructLayout(LayoutKind.Sequential)]
  private struct NativeGraphCalibration
  {
    public IntPtr Sizes;
    public UIntPtr SizeCapacity;
    public UIntPtr SizeCount;
    public IntPtr Labels;
    public UIntPtr LabelCapacity;
    public UIntPtr LabelCount;
    public NativeGraphDurationRatio Team;
    public double WithinP50;
    public double WithinP85;
    public double Growth;
    public uint ReplayedCount;
    public uint Reserved;
  }
}
