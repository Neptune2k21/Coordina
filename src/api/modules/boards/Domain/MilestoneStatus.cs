namespace Coordina.Api.Modules.Boards.Domain;

/// <summary>Where a milestone stands against its date.</summary>
public enum MilestoneStatus
{
  /// <summary>Even the realistic (85%) date is on or before the target.</summary>
  OnTrack,

  /// <summary>Likely on time, but the realistic date is after the target.</summary>
  AtRisk,

  /// <summary>Likely after the target.</summary>
  Late,

  /// <summary>The target has passed and the milestone is not done.</summary>
  Overdue,

  Completed
}
