using System.Globalization;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Unit.Tests.Boards;

public sealed class FocusTimeTests
{
  // Wednesday 18 November 2026.
  private static readonly DateTimeOffset Wednesday = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

  [Theory]
  [InlineData("2026-11-18T09:30:00Z", 0.5)]
  [InlineData("2026-11-18T14:00:00Z", 5.0)]
  // A long day is a working day.
  [InlineData("2026-11-18T21:00:00Z", 6.0)]
  // A whole day later is a day of work, not two.
  [InlineData("2026-11-19T09:00:00Z", 6.0)]
  [InlineData("2026-11-20T09:00:00Z", 12.0)]
  // Two days and the part of Friday that is inside, then nothing on the weekend.
  [InlineData("2026-11-22T12:00:00Z", 15.75)]
  [InlineData("2026-11-25T09:00:00Z", 30.0)]
  public void Between_CountsWorkingTime(string end, double hours)
  {
    Assert.Equal(hours, FocusTime.Between(Wednesday, At(end)), 6);
  }

  [Fact]
  public void Between_BackwardsOrEmpty_IsNothing()
  {
    Assert.Equal(0, FocusTime.Between(Wednesday, Wednesday));
    Assert.Equal(0, FocusTime.Between(Wednesday, Wednesday.AddHours(-3)));
  }

  [Fact]
  public void HoursOf_AddsUpEveryTimeTheCardWasInProgress()
  {
    var card = Card();
    var times = new BoardFocusTimes(
      [card],
      [
        Event(1, card.Id, "TODO", "2026-11-18T08:00:00Z"),
        Event(2, card.Id, "IN_PROGRESS", "2026-11-18T09:00:00Z"),
        Event(3, card.Id, "TODO", "2026-11-18T11:00:00Z"),
        Event(4, card.Id, "IN_PROGRESS", "2026-11-18T13:00:00Z"),
        Event(5, card.Id, "DONE", "2026-11-18T14:00:00Z")
      ],
      Wednesday.AddDays(5));

    Assert.Equal(3, times.HoursOf(card), 6);
    Assert.Equal(Wednesday, times.StartOf(card.Id));
  }

  [Fact]
  public void HoursOf_ACardStillInProgress_CountsUntilNow()
  {
    var card = Card();
    var times = new BoardFocusTimes(
      [card],
      [Event(1, card.Id, "IN_PROGRESS", "2026-11-18T09:00:00Z")],
      Wednesday.AddHours(4));

    Assert.Equal(4, times.HoursOf(card), 6);
  }

  [Fact]
  public void HoursOf_CardsInProgressTogether_ShareTheirPersonsTime()
  {
    var person = Guid.NewGuid();
    var longer = Card(person);
    var shorter = Card(person);
    var elsewhere = Card(Guid.NewGuid());
    var times = new BoardFocusTimes(
      [longer, shorter, elsewhere],
      [
        Event(1, longer.Id, "IN_PROGRESS", "2026-11-18T09:00:00Z"),
        Event(2, shorter.Id, "IN_PROGRESS", "2026-11-18T11:00:00Z"),
        Event(3, elsewhere.Id, "IN_PROGRESS", "2026-11-18T09:00:00Z"),
        Event(4, longer.Id, "DONE", "2026-11-18T13:00:00Z"),
        Event(5, shorter.Id, "DONE", "2026-11-18T13:00:00Z"),
        Event(6, elsewhere.Id, "DONE", "2026-11-18T13:00:00Z")
      ],
      Wednesday.AddDays(1));

    // Two hours alone, then two hours shared.
    Assert.Equal(3, times.HoursOf(longer), 6);
    Assert.Equal(1, times.HoursOf(shorter), 6);
    Assert.Equal(4, times.HoursOf(elsewhere), 6);
  }

  [Fact]
  public void HoursOf_ACardNeverInProgress_IsNothing()
  {
    var card = Card();
    var times = new BoardFocusTimes(
      [card],
      [Event(1, card.Id, "DONE", "2026-11-18T09:00:00Z")],
      Wednesday.AddDays(1));

    Assert.Equal(0, times.HoursOf(card));
    Assert.Null(times.StartOf(card.Id));
  }

  private static DateTimeOffset At(string value) =>
    DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

  private static BoardCardEvent Event(long id, Guid cardId, string status, string at) =>
    new(id, cardId, BoardCardEventKind.StatusChanged, null, status, At(at));

  private static ProjectBoardCard Card(params Guid[] people) => PlanningBoards.Card("Card", people);
}
