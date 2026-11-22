namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>
/// A way to cut a milestone's scope, and what it buys: the chance of shipping
/// by the target date (0 to 1) before and after, and the dates the work left
/// is forecast to be done by. No cut means nothing is worth taking out: the
/// milestone is on track, or no cut it allows would help.
/// </summary>
public sealed record BoardMilestoneScopeResponse(
  Guid MilestoneId,
  DateOnly TargetDate,
  double OnTimeBefore,
  double OnTimeAfter,
  DateOnly ForecastDate,
  DateOnly CommitDate,
  IReadOnlyCollection<BoardMilestoneCutResponse> Cuts);

/// <summary>
/// A card to take out of the milestone, and the cards that leave with it:
/// those that depend on it, and those the milestone then no longer needs.
/// </summary>
public sealed record BoardMilestoneCutResponse(
  Guid CardId,
  string Title,
  IReadOnlyCollection<BoardMilestoneCutCardResponse> AlsoRemoved);

public sealed record BoardMilestoneCutCardResponse(Guid CardId, string Title);
