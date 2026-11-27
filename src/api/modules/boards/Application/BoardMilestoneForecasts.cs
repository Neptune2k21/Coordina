using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>
/// Forecasts every milestone of a board with the graph engine. The engine
/// simulates the work in working days, all milestones sharing the team; this
/// turns them into dates (see <see cref="WorkingDays"/>) and says where each
/// milestone stands against its target.
/// </summary>
internal static class BoardMilestoneForecasts
{
  private const int DriverLimit = 3;

  // A card has to set the date in at least one run in ten to count as a driver.
  private const double MinDriverCriticality = 0.1;

  public static GraphForecast<Guid> Forecast(IGraphEngine<Guid> graphEngine, BoardPlan plan) =>
    graphEngine.Forecast(plan.Graph, BoardPlan.Simulation);

  /// <summary>Every milestone, done ones included, soonest target first.</summary>
  public static BoardMilestoneResponse[] ToResponses(BoardPlan plan, GraphForecast<Guid> forecast)
  {
    var forecasts = forecast.Milestones.ToDictionary(milestone => milestone.Node);

    return plan.Cards.Values
      .Where(card => card.MilestoneDate is not null)
      .OrderBy(card => card.MilestoneDate)
      .ThenBy(card => card.Title, StringComparer.Ordinal)
      .Select(card => forecasts.TryGetValue(card.Id, out var milestone)
        ? ToResponse(plan, card, milestone)
        : Completed(card))
      .ToArray();
  }

  public static ForecastSnapshot[] ToSnapshots(BoardPlan plan, GraphForecast<Guid> forecast) =>
    forecast.Milestones
      .Select(milestone => new ForecastSnapshot(
        milestone.Node,
        plan.Today,
        plan.Cards[milestone.Node].MilestoneDate!.Value,
        plan.Finish(milestone.P50Days),
        plan.Finish(milestone.P85Days),
        milestone.OnTime,
        milestone.CardCount))
      .ToArray();

  private static BoardMilestoneResponse Completed(ProjectBoardCard milestone) =>
    new(
      milestone.Id,
      milestone.Title,
      milestone.MilestoneDate!.Value,
      EnumCode.Format(MilestoneStatus.Completed),
      null,
      null,
      null,
      null,
      null,
      null,
      null,
      0,
      0,
      []);

  private static BoardMilestoneResponse ToResponse(
    BoardPlan plan,
    ProjectBoardCard milestone,
    GraphMilestoneForecast<Guid> forecast)
  {
    var target = milestone.MilestoneDate!.Value;
    var forecastDate = plan.Finish(forecast.P50Days);
    var commitDate = plan.Finish(forecast.P85Days);

    return new BoardMilestoneResponse(
      milestone.Id,
      milestone.Title,
      target,
      EnumCode.Format(StatusOf(target, forecastDate, commitDate, plan.Today)),
      forecastDate,
      commitDate,
      plan.Finish(forecast.P10Days),
      plan.Finish(forecast.P95Days),
      plan.Finish(forecast.ChainDays),
      forecast.OnTime,
      forecastDate.DayNumber - target.DayNumber,
      forecast.CardCount,
      forecast.UnsizedCount,
      forecast.Cards
        .Where(card => card.Node != milestone.Id && card.Criticality >= MinDriverCriticality)
        .Take(DriverLimit)
        .Select(card => new BoardMilestoneDriverResponse(
          card.Node,
          plan.Cards[card.Node].Title,
          card.Criticality,
          card.Sensitivity))
        .ToArray());
  }

  private static MilestoneStatus StatusOf(
    DateOnly target,
    DateOnly forecastDate,
    DateOnly commitDate,
    DateOnly today)
  {
    if (target < today)
    {
      return MilestoneStatus.Overdue;
    }

    if (forecastDate > target)
    {
      return MilestoneStatus.Late;
    }

    return commitDate > target ? MilestoneStatus.AtRisk : MilestoneStatus.OnTrack;
  }
}
