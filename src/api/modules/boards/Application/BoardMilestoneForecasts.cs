using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>
/// Forecasts every milestone of a board with the graph engine. The engine
/// simulates the work in working days; this turns them into dates (see
/// <see cref="WorkingDays"/>) and says where each milestone stands against
/// its target.
/// </summary>
internal static class BoardMilestoneForecasts
{
  // A few thousand runs keep the 85% date steady, and a fixed seed keeps the
  // forecast the same from one load to the next while nothing changes.
  private const int RunCount = 2000;
  private const ulong Seed = 129;
  private const int DriverLimit = 3;

  // A card has to set the date in at least one run in ten to count as a driver.
  private const double MinDriverCriticality = 0.1;

  // The engine ranks the cards the same way for everyone, so the forecast does
  // not depend on who asks; these preferences belong to nobody.
  private static readonly BoardPlanningPreferences Nobody = new(Guid.Empty, null, null, false, true);

  public static BoardMilestoneResponse[] Forecast(
    IGraphEngine<Guid> graphEngine,
    ProjectBoard board,
    DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(graphEngine);
    ArgumentNullException.ThrowIfNull(board);

    var graph = BoardDependencyGraph.ToGraph(board);
    var milestones = graph.Cards
      .Where(card => card.MilestoneDate is not null)
      .OrderBy(card => card.MilestoneDate)
      .ThenBy(card => card.Title, StringComparer.Ordinal)
      .ToArray();

    if (milestones.Length == 0)
    {
      return [];
    }

    var input = new ForecastInput(
      graphEngine,
      graph,
      BoardDependencyGraph.ToWorkItems(board, graph.Cards, today, Nobody),
      TeamOf(graph.Cards),
      graph.Cards.ToDictionary(card => card.Id),
      today);

    return milestones
      .Select(milestone => ToResponse(milestone, input))
      .ToArray();
  }

  private static BoardMilestoneResponse ToResponse(ProjectBoardCard milestone, ForecastInput input)
  {
    var target = milestone.MilestoneDate!.Value;

    if (milestone.IsCompleted)
    {
      return new BoardMilestoneResponse(
        milestone.Id,
        milestone.Title,
        target,
        EnumCode.Format(MilestoneStatus.Completed),
        null,
        null,
        null,
        0,
        0,
        []);
    }

    var forecast = input.GraphEngine.Forecast(
      input.Graph.Nodes,
      input.Graph.Edges,
      input.WorkItems,
      input.Team.Assignees,
      new GraphForecastOptions<Guid>(milestone.Id, input.Team.Size, RunCount, Seed));
    var forecastDate = WorkingDays.Finish(input.Today, forecast.P50Days);
    var commitDate = WorkingDays.Finish(input.Today, forecast.P85Days);

    return new BoardMilestoneResponse(
      milestone.Id,
      milestone.Title,
      target,
      EnumCode.Format(StatusOf(target, forecastDate, commitDate, input.Today)),
      forecastDate,
      commitDate,
      forecastDate.DayNumber - target.DayNumber,
      forecast.Cards.Count,
      forecast.UnsizedCount,
      forecast.Cards
        .Where(card => card.Node != milestone.Id && card.Criticality >= MinDriverCriticality)
        .Take(DriverLimit)
        .Select(card => new BoardMilestoneDriverResponse(
          card.Node,
          input.CardsById[card.Node].Title,
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

  // The team is everyone assigned to an open card of the board, numbered in a
  // stable order; a board where nobody is assigned plans for one person.
  private static ForecastTeam TeamOf(IReadOnlyCollection<ProjectBoardCard> cards)
  {
    var openCards = cards.Where(card => !card.IsCompleted).ToArray();
    var people = openCards
      .SelectMany(card => card.Assignees)
      .Select(assignee => assignee.UserId)
      .Distinct()
      .Order()
      .Select((userId, index) => (userId, index))
      .ToDictionary(person => person.userId, person => person.index);
    var assignees = openCards
      .SelectMany(card => card.Assignees.Select(assignee =>
        new GraphAssignee<Guid>(card.Id, people[assignee.UserId])))
      .ToArray();

    return new ForecastTeam(Math.Max(1, people.Count), assignees);
  }

  private sealed record ForecastTeam(int Size, IReadOnlyCollection<GraphAssignee<Guid>> Assignees);

  private sealed record ForecastInput(
    IGraphEngine<Guid> GraphEngine,
    BoardGraphSnapshot Graph,
    IReadOnlyCollection<GraphWorkItem<Guid>> WorkItems,
    ForecastTeam Team,
    Dictionary<Guid, ProjectBoardCard> CardsById,
    DateOnly Today);
}
