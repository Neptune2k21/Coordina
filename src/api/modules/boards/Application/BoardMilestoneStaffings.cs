using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>
/// Asks the graph engine who should take which card so a milestone ships
/// soonest, or what chosen changes buy. The dates before are those the
/// milestone shows, and the dates after those it shows once applied.
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
      (skilled, options) => graphEngine.ProposeStaffing(
        target.Plan.Graph,
        receivers.Select(target.Plan.PersonOf).ToArray(),
        skilled,
        options));

  public static Result<StaffingPlan> Evaluate(
    IGraphEngine<Guid> graphEngine,
    StaffingBoard target,
    IReadOnlyCollection<BoardCardReassignment> changes) =>
    Run(
      target,
      changes.Select(change => change.UserId).ToArray(),
      false,
      (_, options) => graphEngine.EvaluateStaffing(
        target.Plan.Graph,
        changes
          .Select(change => new GraphAssignee<Guid>(change.CardId, target.Plan.PersonOf(change.UserId)))
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
    Func<GraphAssignee<Guid>[], GraphStaffingOptions<Guid>, GraphStaffing<Guid>> solve)
  {
    if (target.Plan.FindMilestone(target.MilestoneId) is not { MilestoneDate: { } targetDate } milestone)
    {
      return Error.NotFound();
    }

    if (milestone.IsCompleted)
    {
      return Error.Validation("milestoneId", "This milestone is done; there is nothing left to plan.");
    }

    var labelsDone = LabelsDone(target.Plan.Cards.Values);
    var staffing = solve(
      Skilled(target.Plan, labelsDone),
      new GraphStaffingOptions<Guid>(
        milestone.Id,
        BoardPlan.Simulation,
        allowReassignment,
        ScreenRunCount,
        EvaluationLimit,
        TimeLimit));

    return Result.Success(new StaffingPlan(milestone, targetDate, target, staffing, receivers, labelsDone));
  }

  // A person suits an open card when they have completed cards sharing one
  // of its labels.
  private static GraphAssignee<Guid>[] Skilled(
    BoardPlan plan,
    Dictionary<Guid, HashSet<string>> labelsDone) =>
    plan.Cards.Values
      .Where(card => !card.IsCompleted && card.Labels.Count > 0)
      .SelectMany(card => plan.People
        .Where(userId => labelsDone.TryGetValue(userId, out var labels) && card.Labels.Any(labels.Contains))
        .Select(userId => new GraphAssignee<Guid>(card.Id, plan.PersonOf(userId))))
      .ToArray();

  // The labels of the cards each person has completed: those they were
  // assigned to, or completed themselves when nobody was.
  private static Dictionary<Guid, HashSet<string>> LabelsDone(IEnumerable<ProjectBoardCard> cards) =>
    cards
      .Where(card => card.IsCompleted && card.Labels.Count > 0)
      .SelectMany(card => BoardPlans.DoneBy(card).Select(userId => (userId, card.Labels)))
      .GroupBy(done => done.userId, done => done.Labels)
      .ToDictionary(
        group => group.Key,
        group => group.SelectMany(labels => labels).ToHashSet(StringComparer.Ordinal));
}

/// <summary>The milestone a staffing plan is about, and who the workspace's members are.</summary>
internal sealed record StaffingBoard(
  BoardPlan Plan,
  Guid MilestoneId,
  IReadOnlyDictionary<Guid, BoardCardAssigneeResponse> Members);

/// <summary>A plan the engine proposed or measured, and the board it was measured on.</summary>
internal sealed record StaffingPlan(
  ProjectBoardCard Milestone,
  DateOnly TargetDate,
  StaffingBoard Target,
  GraphStaffing<Guid> Engine,
  IReadOnlyCollection<Guid> Receivers,
  Dictionary<Guid, HashSet<string>> LabelsDone)
{
  public BoardMilestoneStaffingResponse ToResponse()
  {
    var plan = Target.Plan;

    return new BoardMilestoneStaffingResponse(
      Milestone.Id,
      TargetDate,
      plan.Finish(Engine.P50DaysBefore),
      plan.Finish(Engine.P85DaysBefore),
      plan.Finish(Engine.P50DaysAfter),
      plan.Finish(Engine.P85DaysAfter),
      Math.Max(0, Engine.OthersLateDaysAfter - Engine.OthersLateDaysBefore),
      Receivers.Select(PersonResponse).ToArray(),
      Engine.Changes.Select(ToResponse).ToArray());
  }

  private BoardMilestoneStaffChangeResponse ToResponse(GraphStaffChange<Guid> change)
  {
    var card = Target.Plan.Cards[change.Node];
    var userId = Target.Plan.People[change.Person];
    var done = LabelsDone.GetValueOrDefault(userId);

    return new BoardMilestoneStaffChangeResponse(
      card.Id,
      card.Title,
      card.Assignees
        .Select(assignee => new BoardCardAssigneeResponse(assignee.UserId, assignee.Name, assignee.Email))
        .ToArray(),
      PersonResponse(userId),
      Target.Plan.Start(change.StartDaysBefore),
      Target.Plan.Start(change.StartDaysAfter),
      done is null ? [] : card.Labels.Where(done.Contains).ToArray());
  }

  private BoardCardAssigneeResponse PersonResponse(Guid userId) =>
    Target.Members.GetValueOrDefault(userId) ?? new BoardCardAssigneeResponse(userId, null, null);
}
