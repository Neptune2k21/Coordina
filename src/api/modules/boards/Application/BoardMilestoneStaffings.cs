using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>
/// Asks the graph engine who should take which card so a milestone ships
/// soonest, or what chosen changes buy. Both run on the forecast's draws, and
/// people are numbered in the forecast's order, so the dates before are those
/// the milestone shows and the dates after those it shows once applied.
/// </summary>
internal static class BoardMilestoneStaffings
{
  private const int ScreenRunCount = 8;
  private const int EvaluationLimit = 20000;

  // Measured on the 500-card benchmark with eight people: the whole proposal
  // fits in this, under the second a person waits for it.
  private static readonly TimeSpan TimeLimit = TimeSpan.FromMilliseconds(800);

  public static Result<StaffingPlan> Propose(
    IGraphEngine<Guid> graphEngine,
    StaffingBoard target,
    IReadOnlyCollection<Guid> receivers,
    bool allowReassignment) =>
    Run(
      target,
      receivers,
      allowReassignment,
      (input, options) => graphEngine.ProposeStaffing(
        input.Graph.Nodes,
        input.Graph.Edges,
        input.WorkItems,
        input.Assignees,
        receivers.Select(input.PersonOf).ToArray(),
        input.Skilled,
        options));

  public static Result<StaffingPlan> Evaluate(
    IGraphEngine<Guid> graphEngine,
    StaffingBoard target,
    IReadOnlyCollection<BoardCardReassignment> changes) =>
    Run(
      target,
      changes.Select(change => change.UserId).ToArray(),
      false,
      (input, options) => graphEngine.EvaluateStaffing(
        input.Graph.Nodes,
        input.Graph.Edges,
        input.WorkItems,
        input.Assignees,
        changes
          .Select(change => new GraphAssignee<Guid>(change.CardId, input.PersonOf(change.UserId)))
          .ToArray(),
        options));

  /// <summary>The people assigned to an open card of the board.</summary>
  public static IReadOnlyCollection<Guid> TeamOf(ProjectBoard board) =>
    board.Lists
      .SelectMany(list => list.Cards)
      .Where(card => !card.IsCompleted)
      .SelectMany(card => card.Assignees)
      .Select(assignee => assignee.UserId)
      .Distinct()
      .ToArray();

  private static Result<StaffingPlan> Run(
    StaffingBoard target,
    IReadOnlyCollection<Guid> receivers,
    bool allowReassignment,
    Func<StaffingInput, GraphStaffingOptions<Guid>, GraphStaffing<Guid>> solve)
  {
    var graph = BoardDependencyGraph.ToGraph(target.Board);
    var milestone = graph.Cards.FirstOrDefault(card => card.Id == target.MilestoneId);

    if (milestone?.MilestoneDate is not { } targetDate)
    {
      return Error.NotFound();
    }

    if (milestone.IsCompleted)
    {
      return Error.Validation("milestoneId", "This milestone is done; there is nothing left to plan.");
    }

    // Numbered in one order, the forecast's team keeps its own order among
    // everyone else.
    var people = TeamOf(target.Board)
      .Concat(receivers)
      .Distinct()
      .Order()
      .ToArray();
    var labelsDone = LabelsDone(graph.Cards);
    var input = new StaffingInput(
      graph,
      BoardDependencyGraph.ToWorkItems(target.Board, graph.Cards, target.Today, BoardMilestoneForecasts.Nobody),
      people.Select((userId, index) => (userId, index)).ToDictionary(),
      labelsDone);
    var staffing = solve(
      input,
      new GraphStaffingOptions<Guid>(
        milestone.Id,
        people.Length,
        allowReassignment,
        BoardMilestoneForecasts.RunCount,
        ScreenRunCount,
        BoardMilestoneForecasts.Seed,
        EvaluationLimit,
        TimeLimit));

    return Result.Success(new StaffingPlan(
      milestone,
      targetDate,
      target,
      staffing,
      people,
      receivers,
      graph.Cards.ToDictionary(card => card.Id),
      labelsDone));
  }

  // The labels of the cards each person has completed: those they were
  // assigned to, or completed themselves when nobody was.
  private static Dictionary<Guid, HashSet<string>> LabelsDone(IReadOnlyCollection<ProjectBoardCard> cards) =>
    cards
      .Where(card => card.IsCompleted && card.Labels.Count > 0)
      .SelectMany(card => DoneBy(card).Select(userId => (userId, card.Labels)))
      .GroupBy(done => done.userId, done => done.Labels)
      .ToDictionary(
        group => group.Key,
        group => group.SelectMany(labels => labels).ToHashSet(StringComparer.Ordinal));

  private static IEnumerable<Guid> DoneBy(ProjectBoardCard card) =>
    card.Assignees.Count > 0
      ? card.Assignees.Select(assignee => assignee.UserId)
      : card.CompletedBy is { } completedBy ? [completedBy.UserId] : [];

  private sealed class StaffingInput(
    BoardGraphSnapshot graph,
    IReadOnlyCollection<GraphWorkItem<Guid>> workItems,
    Dictionary<Guid, int> personByUser,
    Dictionary<Guid, HashSet<string>> labelsDone)
  {
    public BoardGraphSnapshot Graph { get; } = graph;

    public IReadOnlyCollection<GraphWorkItem<Guid>> WorkItems { get; } = workItems;

    public IReadOnlyCollection<GraphAssignee<Guid>> Assignees { get; } = graph.Cards
      .Where(card => !card.IsCompleted)
      .SelectMany(card => card.Assignees.Select(assignee =>
        new GraphAssignee<Guid>(card.Id, personByUser[assignee.UserId])))
      .ToArray();

    // A person suits an open card when they have completed cards sharing
    // one of its labels.
    public IReadOnlyCollection<GraphAssignee<Guid>> Skilled { get; } = graph.Cards
      .Where(card => !card.IsCompleted && card.Labels.Count > 0)
      .SelectMany(card => personByUser
        .Where(person => labelsDone.TryGetValue(person.Key, out var labels) && card.Labels.Any(labels.Contains))
        .Select(person => new GraphAssignee<Guid>(card.Id, person.Value)))
      .ToArray();

    public int PersonOf(Guid userId) => personByUser[userId];
  }
}

/// <summary>The board a staffing plan is about, and the day it is planned from.</summary>
internal sealed record StaffingBoard(
  ProjectBoard Board,
  Guid MilestoneId,
  IReadOnlyDictionary<Guid, BoardCardAssigneeResponse> Members,
  DateOnly Today);

/// <summary>A plan the engine proposed or measured, and the board it was measured on.</summary>
internal sealed record StaffingPlan(
  ProjectBoardCard Milestone,
  DateOnly TargetDate,
  StaffingBoard Target,
  GraphStaffing<Guid> Engine,
  IReadOnlyList<Guid> People,
  IReadOnlyCollection<Guid> Receivers,
  Dictionary<Guid, ProjectBoardCard> CardsById,
  Dictionary<Guid, HashSet<string>> LabelsDone)
{
  public BoardMilestoneStaffingResponse ToResponse()
  {
    var today = Target.Today;

    return new BoardMilestoneStaffingResponse(
      Milestone.Id,
      TargetDate,
      WorkingDays.Finish(today, Engine.P50DaysBefore),
      WorkingDays.Finish(today, Engine.P85DaysBefore),
      WorkingDays.Finish(today, Engine.P50DaysAfter),
      WorkingDays.Finish(today, Engine.P85DaysAfter),
      Receivers.Select(PersonResponse).ToArray(),
      Engine.Changes.Select(ToResponse).ToArray());
  }

  private BoardMilestoneStaffChangeResponse ToResponse(GraphStaffChange<Guid> change)
  {
    var card = CardsById[change.Node];
    var userId = People[change.Person];
    var done = LabelsDone.GetValueOrDefault(userId);

    return new BoardMilestoneStaffChangeResponse(
      card.Id,
      card.Title,
      card.Assignees
        .Select(assignee => new BoardCardAssigneeResponse(assignee.UserId, assignee.Name, assignee.Email))
        .ToArray(),
      PersonResponse(userId),
      WorkingDays.Start(Target.Today, change.StartDaysBefore),
      WorkingDays.Start(Target.Today, change.StartDaysAfter),
      done is null ? [] : card.Labels.Where(done.Contains).ToArray());
  }

  private BoardCardAssigneeResponse PersonResponse(Guid userId) =>
    Target.Members.GetValueOrDefault(userId) ?? new BoardCardAssigneeResponse(userId, null, null);
}
