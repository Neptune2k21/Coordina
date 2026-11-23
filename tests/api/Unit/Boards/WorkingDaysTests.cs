using System.Globalization;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Tests.Unit.Boards;

public sealed class WorkingDaysTests
{
  // Wednesday 18 November 2026.
  private static readonly DateOnly Wednesday = new(2026, 11, 18);

  [Theory]
  [InlineData(0.0, "2026-11-18")]
  [InlineData(0.4, "2026-11-18")]
  [InlineData(1.0, "2026-11-18")]
  [InlineData(1.2, "2026-11-19")]
  [InlineData(3.0, "2026-11-20")]
  [InlineData(4.0, "2026-11-23")]
  [InlineData(10.0, "2026-12-01")]
  public void Finish_CountsWeekdaysOnly(double workingDays, string expected)
  {
    Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), WorkingDays.Finish(Wednesday, workingDays));
  }

  [Fact]
  public void Finish_FromAWeekend_StartsOnMonday()
  {
    var saturday = new DateOnly(2026, 11, 21);

    Assert.Equal(new DateOnly(2026, 11, 23), WorkingDays.Finish(saturday, 1.0));
    Assert.Equal(new DateOnly(2026, 11, 27), WorkingDays.Finish(saturday.AddDays(1), 5.0));
  }

  [Fact]
  public void Finish_ForgivesRoundingNoise()
  {
    Assert.Equal(Wednesday.AddDays(1), WorkingDays.Finish(Wednesday, 2.0 + 1e-12));
  }

  [Theory]
  [InlineData(0.0, "2026-11-18")]
  [InlineData(0.9, "2026-11-18")]
  [InlineData(1.0, "2026-11-19")]
  [InlineData(1.0 - 1e-12, "2026-11-19")]
  [InlineData(3.0, "2026-11-23")]
  public void Start_IsTheWorkingDayAfterTheWorkBeforeIt(double workingDays, string expected)
  {
    Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), WorkingDays.Start(Wednesday, workingDays));
  }

  [Theory]
  [InlineData("2026-11-17", 0)]
  [InlineData("2026-11-18", 1)]
  [InlineData("2026-11-20", 3)]
  [InlineData("2026-11-22", 3)]
  [InlineData("2026-11-23", 4)]
  [InlineData("2026-12-01", 10)]
  public void Until_CountsTheWeekdaysUpToTheTarget(string target, int expected)
  {
    Assert.Equal(expected, WorkingDays.Until(Wednesday, DateOnly.Parse(target, CultureInfo.InvariantCulture)));
  }

  [Fact]
  public void Until_IsTheLongestStretchThatStillFinishesByTheTarget()
  {
    var target = new DateOnly(2026, 12, 4);
    var days = WorkingDays.Until(Wednesday, target);

    Assert.True(WorkingDays.Finish(Wednesday, days) <= target);
    Assert.True(WorkingDays.Finish(Wednesday, days + 0.1) > target);
  }
}
