using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application.Planning;

/// <summary>Turns a board and its history into what the engine plans.</summary>
internal static class BoardPlans
{
  // The engine ranks the cards the same way for everyone, so a forecast does
  // not depend on who asks; these preferences belong to nobody.
  private static readonly BoardPlanningPreferences Nobody = new(Guid.Empty, null, null, false, true);

  /// <param name="board">The board to plan.</param>
  /// <param name="newcomers">People who may be given cards besides those already on the board.</param>
  /// <param name="now">When the plan starts from.</param>
  public static BoardPlan Build(
    ProjectBoard board,
    IReadOnlyCollection<Guid> newcomers,
    DateTimeOffset now)
  {
    var today = DateOnly.FromDateTime(now.UtcDateTime);
    var graph = BoardDependencyGraph.ToGraph(board);
    var cards = graph.Cards.ToDictionary(card => card.Id);
    // Numbered in one order, so naming a newcomer moves nobody else.
    var people = graph.Cards
      .SelectMany(DoneBy)
      .Concat(newcomers)
      .Distinct()
      .Order()
      .ToArray();
    var personByUser = Numbered(people);
    var labels = graph.Cards
      .SelectMany(card => card.Labels)
      .Distinct(StringComparer.Ordinal)
      .Order(StringComparer.Ordinal)
      .ToArray();
    var labelNumbers = Numbered(labels);
    var milestones = OpenMilestones(graph.Cards);

    return new BoardPlan(
      board,
      new GraphBoard<Guid>(
        graph.Nodes,
        graph.Edges,
        BoardDependencyGraph.ToWorkItems(board, graph.Cards, today, Nobody),
        graph.Cards
          .SelectMany(card => DoneBy(card).Select(userId => new GraphAssignee<Guid>(card.Id, personByUser[userId])))
          .ToArray(),
        graph.Cards
          .SelectMany(card => card.Labels.Select(label => new GraphCardLabel<Guid>(card.Id, labelNumbers[label])))
          .ToArray(),
        milestones.Select(card => ToMilestone(card, today)).ToArray(),
        [],
        GraphScopeGrowth.None,
        people.Length),
      cards,
      people,
      labels,
      today);
  }

  /// <summary>Open milestones, soonest target first.</summary>
  public static ProjectBoardCard[] OpenMilestones(IEnumerable<ProjectBoardCard> cards) =>
    cards
      .Where(card => card.MilestoneDate is not null && !card.IsCompleted)
      .OrderBy(card => card.MilestoneDate)
      .ThenBy(card => card.Title, StringComparer.Ordinal)
      .ToArray();

  /// <summary>Who did a card, or is on it: whoever completed it when nobody was assigned.</summary>
  public static IEnumerable<Guid> DoneBy(ProjectBoardCard card) =>
    card.Assignees.Count == 0 && card.IsCompleted && card.CompletedBy is { } completedBy
      ? [completedBy.UserId]
      : card.Assignees.Select(assignee => assignee.UserId);

  /// <summary>The focus hours from today to the end of the target day; negative once it has passed.</summary>
  public static double TargetHours(DateOnly today, DateOnly target)
  {
    var days = target >= today
      ? WorkingDays.Until(today, target)
      : -WorkingDays.Until(target.AddDays(1), today.AddDays(-1));

    return days * WorkingDays.FocusHoursPerDay;
  }

  private static GraphMilestone<Guid> ToMilestone(ProjectBoardCard card, DateOnly today) =>
    new(card.Id, TargetHours(today, card.MilestoneDate!.Value), WeightOf(card.Priority));

  // What being late on a milestone costs, as the engine weighs a card's value.
  private static double WeightOf(BoardCardPriority? priority) =>
    priority switch
    {
      BoardCardPriority.High => 4,
      BoardCardPriority.Medium => 2,
      _ => 1
    };

  private static Dictionary<T, int> Numbered<T>(IReadOnlyCollection<T> values)
    where T : notnull =>
    values
      .Select((value, index) => (value, index))
      .ToDictionary(entry => entry.value, entry => entry.index);
}
