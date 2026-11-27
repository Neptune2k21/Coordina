using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Modules.Boards.Application.Planning;

/// <summary>
/// How long cards really took, read back from their status changes. Someone
/// with three cards in progress gives a third of their time to each, so cards
/// started together are not counted three times over.
/// </summary>
internal sealed class BoardFocusTimes
{
  private readonly Dictionary<Guid, Span[]> _spansByCard;
  private readonly Dictionary<Guid, SharedAttention> _attentionByPerson;

  public BoardFocusTimes(
    IReadOnlyCollection<ProjectBoardCard> cards,
    IReadOnlyCollection<BoardCardEvent> events,
    DateTimeOffset now)
  {
    _spansByCard = events
      .GroupBy(cardEvent => cardEvent.CardId)
      .ToDictionary(group => group.Key, group => SpansOf(group, now));
    _attentionByPerson = cards
      .SelectMany(card => card.Assignees
        .SelectMany(assignee => SpansOf(card.Id).Select(span => (assignee.UserId, span))))
      .GroupBy(entry => entry.UserId, entry => entry.span)
      .ToDictionary(group => group.Key, group => new SharedAttention([.. group]));
  }

  /// <summary>When work on the card first started, if it has.</summary>
  public DateTimeOffset? StartOf(Guid cardId) =>
    SpansOf(cardId) is [var first, ..] ? first.Start : null;

  /// <summary>The focus hours the card took, or has taken so far; 0 when it was never in progress.</summary>
  public double HoursOf(ProjectBoardCard card) =>
    SpansOf(card.Id).Sum(span => FocusTime.Between(span.Start, span.End) * ShareOf(card, span));

  private Span[] SpansOf(Guid cardId) => _spansByCard.GetValueOrDefault(cardId) ?? [];

  // The part of the card's people's attention it had, on average over the span.
  private double ShareOf(ProjectBoardCard card, Span span) =>
    card.Assignees.Count == 0
      ? 1
      : card.Assignees.Average(assignee => _attentionByPerson[assignee.UserId].ShareOf(span));

  private static Span[] SpansOf(IEnumerable<BoardCardEvent> events, DateTimeOffset now)
  {
    var inProgress = EnumCode.Format(BoardTaskStatus.InProgress);
    var spans = new List<Span>();
    DateTimeOffset? start = null;

    foreach (var change in events.OrderBy(cardEvent => cardEvent.Id))
    {
      if (change.To == inProgress)
      {
        start ??= change.OccurredAt;
      }
      else if (start is { } started)
      {
        AddSpan(spans, started, change.OccurredAt);
        start = null;
      }
    }

    if (start is { } open)
    {
      AddSpan(spans, open, now);
    }

    return [.. spans];
  }

  private static void AddSpan(List<Span> spans, DateTimeOffset start, DateTimeOffset end)
  {
    if (end > start)
    {
      spans.Add(new Span(start, end));
    }
  }

  private sealed record Span(DateTimeOffset Start, DateTimeOffset End);

  /// <summary>
  /// One person's cards in progress over time. The time each card gets, one
  /// part in as many as there are, is added up once in time order, so a span's
  /// share is the difference between its two ends.
  /// </summary>
  private sealed class SharedAttention
  {
    private readonly Dictionary<DateTimeOffset, TimeSpan> _givenUntil = [];

    public SharedAttention(Span[] spans)
    {
      var changes = spans
        .SelectMany(span => new[] { (At: span.Start, Cards: 1), (At: span.End, Cards: -1) })
        .GroupBy(change => change.At, change => change.Cards)
        .OrderBy(group => group.Key);
      var cards = 0;
      var given = TimeSpan.Zero;
      DateTimeOffset? previous = null;

      foreach (var change in changes)
      {
        if (cards > 0 && previous is { } since)
        {
          given += (change.Key - since) / cards;
        }

        _givenUntil[change.Key] = given;
        cards += change.Sum();
        previous = change.Key;
      }
    }

    public double ShareOf(Span span) =>
      (_givenUntil[span.End] - _givenUntil[span.Start]) / (span.End - span.Start);
  }
}
