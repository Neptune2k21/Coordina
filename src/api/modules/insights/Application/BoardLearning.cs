using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Insights.Contracts;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Modules.Insights.Application;

/// <summary>What a board's history says about its durations, and about its past forecasts.</summary>
internal static class BoardLearning
{
  public static BoardLearningResponse ToResponse(
    BoardPlan plan,
    GraphCalibration calibration,
    IReadOnlyCollection<ForecastSnapshot> snapshots) =>
    new(
      ToResponse(null, calibration.Team),
      calibration.Sizes
        .Where(ratio => ratio.CardCount > 0)
        .Select(ratio => ToResponse(EnumCode.Format((CardSize)ratio.Key), ratio))
        .ToArray(),
      calibration.Labels
        .Select(ratio => ToResponse(plan.Labels[ratio.Key], ratio))
        .ToArray(),
      calibration.Growth,
      calibration.ReplayedCount == 0
        ? null
        : new BoardCardAccuracyResponse(
          calibration.ReplayedCount,
          calibration.WithinP50,
          calibration.WithinP85),
      Accuracy(plan, snapshots));

  // Days with a forecast are not independent, so each milestone counts once,
  // whether it took a week or a year.
  private static BoardMilestoneAccuracyResponse? Accuracy(
    BoardPlan plan,
    IReadOnlyCollection<ForecastSnapshot> snapshots)
  {
    var milestones = snapshots
      .GroupBy(snapshot => snapshot.MilestoneId)
      .Select(group => Checked(plan.Cards.GetValueOrDefault(group.Key), group))
      .OfType<CheckedMilestone>()
      .ToArray();

    return milestones.Length == 0
      ? null
      : new BoardMilestoneAccuracyResponse(
        milestones.Length,
        milestones.Sum(milestone => milestone.SnapshotCount),
        milestones.Average(milestone => milestone.ForecastMet),
        milestones.Average(milestone => milestone.CommitMet),
        milestones.Average(milestone => milestone.MeanSlipDays));
  }

  private static CheckedMilestone? Checked(
    ProjectBoardCard? milestone,
    IEnumerable<ForecastSnapshot> snapshots)
  {
    if (milestone is not { IsCompleted: true, CompletedAt: { } completedAt })
    {
      return null;
    }

    var done = DateOnly.FromDateTime(completedAt.UtcDateTime);
    var before = snapshots.Where(snapshot => snapshot.TakenOn <= done).ToArray();

    return before.Length == 0
      ? null
      : new CheckedMilestone(
        before.Length,
        before.Average(snapshot => done <= snapshot.ForecastDate ? 1.0 : 0.0),
        before.Average(snapshot => done <= snapshot.CommitDate ? 1.0 : 0.0),
        before.Average(snapshot => done.DayNumber - snapshot.ForecastDate.DayNumber));
  }

  private static BoardDurationRatioResponse ToResponse(string? name, GraphDurationRatio ratio) =>
    new(name, ratio.Low, ratio.Median, ratio.High, ratio.CardCount);

  private sealed record CheckedMilestone(
    int SnapshotCount,
    double ForecastMet,
    double CommitMet,
    double MeanSlipDays);
}
