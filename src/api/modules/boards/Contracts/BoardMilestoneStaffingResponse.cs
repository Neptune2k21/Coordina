namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>
/// Who should take which card, and what it buys: the dates the milestone's
/// work is forecast to be done by (one chance in two, and 85%) before and
/// after. The dates after are the forecast the milestone shows once the
/// changes are applied. <paramref name="OthersDelayDays"/> is how many days
/// later the board's other milestones end up on average, weighted. No change means no plan the request allows would
/// bring a date in.
/// </summary>
public sealed record BoardMilestoneStaffingResponse(
  Guid MilestoneId,
  DateOnly TargetDate,
  DateOnly ForecastDateBefore,
  DateOnly CommitDateBefore,
  DateOnly ForecastDateAfter,
  DateOnly CommitDateAfter,
  double OthersDelayDays,
  IReadOnlyCollection<BoardCardAssigneeResponse> People,
  IReadOnlyCollection<BoardMilestoneStaffChangeResponse> Changes);

/// <summary>
/// A card to give to someone, who becomes its only assignee: who has it now
/// (nobody when empty), the day it starts before and after if every card
/// takes its likely time, and the card's labels its new assignee has already
/// completed cards with.
/// </summary>
public sealed record BoardMilestoneStaffChangeResponse(
  Guid CardId,
  string Title,
  IReadOnlyCollection<BoardCardAssigneeResponse> From,
  BoardCardAssigneeResponse To,
  DateOnly StartBefore,
  DateOnly StartAfter,
  IReadOnlyCollection<string> MatchingLabels);
