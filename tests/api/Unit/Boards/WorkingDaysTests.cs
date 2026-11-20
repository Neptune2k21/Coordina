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
}
