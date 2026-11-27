using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Insights.Contracts;

/// <summary>
/// What the board's history has taught its forecasts, and how right they have
/// been. People are learned from too, but never compared: nothing here is
/// about one person.
/// </summary>
public sealed record BoardLearningResponse(
  BoardDurationRatioResponse Team,
  IReadOnlyCollection<BoardDurationRatioResponse> Sizes,
  IReadOnlyCollection<BoardDurationRatioResponse> Labels,
  double NewCardsPerCompletedCard,
  BoardCardAccuracyResponse? Cards,
  BoardMilestoneAccuracyResponse? Milestones);

/// <summary>
/// How long cards take against what their size says: 1.6 is 60% longer, with
/// one chance in ten of being below <paramref name="Low"/> and one in ten above
/// <paramref name="High"/>. The name is a size or a label, null for the team.
/// </summary>
public sealed record BoardDurationRatioResponse(
  string? Name,
  double Low,
  double Median,
  double High,
  int CardCount);

/// <summary>
/// Completed cards, each predicted from the ones before it: the share that took
/// no longer than its 50% and 85% durations. Close to 0.5 and 0.85 is right.
/// </summary>
public sealed record BoardCardAccuracyResponse(int CardCount, double WithinHalf, double WithinCommit);

/// <summary>
/// Completed milestones, checked against what their forecasts said every day:
/// the share of those days whose dates were met, one milestone counting as much
/// as another, and how many days later than forecast they were done on average.
/// </summary>
public sealed record BoardMilestoneAccuracyResponse(
  int MilestoneCount,
  int SnapshotCount,
  double ForecastMet,
  double CommitMet,
  double MeanSlipDays);

/// <summary>
/// A page of what happened to the board's cards, oldest first.
/// <paramref name="NextAfter"/> asks for the next page; null when there is none.
/// </summary>
public sealed record BoardEventPageResponse(
  IReadOnlyCollection<BoardCardEventResponse> Events,
  long? NextAfter);

/// <summary>
/// Something that happened to a card. Values are task statuses, sizes, dates or
/// ids, depending on the kind.
/// </summary>
public sealed record BoardCardEventResponse(
  long Id,
  Guid CardId,
  [property: EnumCodes<BoardCardEventKind>] string Kind,
  string? From,
  string? To,
  DateTimeOffset OccurredAt);
