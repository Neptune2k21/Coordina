using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Insights.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Insights.Contracts;

/// <summary>
/// Everything the engine knows about a board's open milestones, in one answer
/// made to be read by a person or an assistant: the dates, who is expected to
/// take which card when, and what the forecast assumes.
/// </summary>
public sealed record BoardPlanResponse(
  Guid BoardId,
  DateOnly Today,
  IReadOnlyCollection<BoardMilestoneResponse> Milestones,
  IReadOnlyCollection<BoardPlannedCardResponse> Cards,
  BoardPlanAssumptionsResponse Assumptions);

/// <summary>
/// An open card of a milestone's scope, in the order to pick cards up: who is
/// expected to take it (nobody in particular when null) and the days it starts
/// and ends if every card takes its median time.
/// </summary>
public sealed record BoardPlannedCardResponse(
  Guid CardId,
  string Title,
  int Order,
  Guid? UserId,
  DateOnly Start,
  DateOnly Finish,
  bool IsInProgress,
  double ElapsedHours,
  IReadOnlyCollection<Guid> MilestoneIds);

/// <summary>
/// What the dates rest on: how many simulated runs, the focus hours a person
/// gives to cards in a working day, how many completed cards the durations were
/// learned from, and how many new cards each completed one has uncovered.
/// </summary>
public sealed record BoardPlanAssumptionsResponse(
  int RunCount,
  double FocusHoursPerDay,
  int LearnedFromCards,
  double NewCardsPerCompletedCard);

/// <summary>
/// One thing to do about a milestone, and what it buys. <paramref name="UserId"/>
/// is who to give the card to; <paramref name="AlsoRemoved"/> how many cards
/// leave with the one to cut, and <paramref name="ValueLost"/> what they were
/// worth by their priorities (high 4, medium 2, otherwise 1). Adding a person
/// names neither.
/// </summary>
public sealed record BoardMilestoneActionResponse(
  [property: EnumCodes<MilestoneActionKind>] string Kind,
  Guid? CardId,
  string? Title,
  Guid? UserId,
  int AlsoRemoved,
  double OnTimeBefore,
  double OnTimeAfter,
  DateOnly CommitDateBefore,
  DateOnly CommitDateAfter,
  double OthersDelayDays,
  double ValueLost);

/// <summary>
/// What to ask the team, most worth asking first, and the days between the
/// milestone's 50% and 85% dates as things are.
/// </summary>
public sealed record BoardMilestoneQuestionsResponse(
  Guid MilestoneId,
  double MarginDays,
  IReadOnlyCollection<BoardMilestoneQuestionResponse> Questions);

/// <summary>
/// A question about a card, and the days its answer is expected to take off the
/// margin. A dependency question names the card it may wait for, how likely
/// that is, and how many days later the milestone would be.
/// </summary>
public sealed record BoardMilestoneQuestionResponse(
  [property: EnumCodes<MilestoneQuestionKind>] string Kind,
  Guid CardId,
  string Title,
  Guid? DependsOnCardId,
  string? DependsOnTitle,
  double? Probability,
  double? ShiftDays,
  double MarginDaysSaved);

/// <summary>
/// Dependencies someone suspects, such as an assistant that has read the
/// specifications, each from 0 to 1. They are weighed against the engine's own
/// guesses; nothing is added to the board.
/// </summary>
public sealed record BoardMilestoneQuestionsRequest(
  IReadOnlyCollection<BoardDependencyHintRequest>? DependencyHints);

public sealed record BoardDependencyHintRequest(Guid CardId, Guid DependsOnCardId, double Probability);
