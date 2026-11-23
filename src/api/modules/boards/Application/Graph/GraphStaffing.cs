namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>
/// How to look for who should take which card: everyone the plan involves
/// (<paramref name="PersonCount"/>), whether cards that already have
/// assignees may move, and the search's budget. The same seed gives the same
/// plan, whatever the thread count, unless the <paramref name="TimeLimit"/>
/// stops the search first.
/// </summary>
public sealed record GraphStaffingOptions<TNode>(
  TNode Milestone,
  int PersonCount,
  bool AllowReassignment,
  int RunCount,
  int ScreenRunCount,
  ulong Seed,
  int EvaluationLimit,
  TimeSpan TimeLimit,
  int ThreadCount = 0)
  where TNode : notnull;

/// <summary>
/// Cards to give to one person each, the first to start first, and the
/// working days until the scope is done before and after: one chance in two,
/// and 85%.
/// </summary>
public sealed record GraphStaffing<TNode>(
  IReadOnlyList<GraphStaffChange<TNode>> Changes,
  double P50DaysBefore,
  double P85DaysBefore,
  double P50DaysAfter,
  double P85DaysAfter)
  where TNode : notnull;

/// <summary>
/// A card and the person who becomes its only assignee, with the working days
/// from now until it starts, before and after, if every card takes its likely time.
/// </summary>
public sealed record GraphStaffChange<TNode>(
  TNode Node,
  int Person,
  double StartDaysBefore,
  double StartDaysAfter)
  where TNode : notnull;
