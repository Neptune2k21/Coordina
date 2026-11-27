namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>
/// How to look for who should take which card of a milestone: whether cards
/// that already have assignees may move, and the search's budget. Only the
/// <paramref name="TimeLimit"/> makes the result depend on the machine.
/// </summary>
public sealed record GraphStaffingOptions<TNode>(
  TNode Milestone,
  GraphSimulation Simulation,
  bool AllowReassignment,
  int ScreenRunCount,
  int EvaluationLimit,
  TimeSpan TimeLimit)
  where TNode : notnull;

/// <summary>
/// Cards to give to one person each, the first to start first, the milestone's
/// forecast now and once the changes are applied, and how late the other
/// milestones are on average before and after.
/// </summary>
public sealed record GraphStaffing<TNode>(
  IReadOnlyList<GraphStaffChange<TNode>> Changes,
  double P50DaysBefore,
  double P85DaysBefore,
  double P50DaysAfter,
  double P85DaysAfter,
  double OthersLateDaysBefore,
  double OthersLateDaysAfter)
  where TNode : notnull;

/// <summary>
/// A card and the person who becomes its only assignee, with the working days
/// from now until it starts, before and after, if every card takes its median time.
/// </summary>
public sealed record GraphStaffChange<TNode>(
  TNode Node,
  int Person,
  double StartDaysBefore,
  double StartDaysAfter)
  where TNode : notnull;
