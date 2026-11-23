namespace Coordina.Api.Modules.Boards.Domain;

/// <summary>
/// Turns the working days a forecast counts into dates: Monday to Friday, and
/// a day's work ends on that day, so one working day from today is today.
/// </summary>
public static class WorkingDays
{
  /// <summary>The focus hours a person works in a day, as the graph engine plans them.</summary>
  public const double FocusHoursPerDay = 6;

  /// <summary>The day work resumes: the day itself, or Monday after a weekend.</summary>
  public static DateOnly FirstOnOrAfter(DateOnly day) =>
    day.DayOfWeek switch
    {
      DayOfWeek.Saturday => day.AddDays(2),
      DayOfWeek.Sunday => day.AddDays(1),
      _ => day
    };

  /// <summary>
  /// The working days from <paramref name="today"/> to the end of
  /// <paramref name="target"/>: the longest stretch that still ends
  /// (<see cref="Finish"/>) on or before it. 0 when the target has passed.
  /// </summary>
  public static int Until(DateOnly today, DateOnly target)
  {
    var count = 0;

    for (var day = FirstOnOrAfter(today); day <= target; day = day.AddDays(1))
    {
      count += day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0 : 1;
    }

    return count;
  }

  /// <summary>The date work starts once <paramref name="workingDays"/> of work from <paramref name="today"/> are done.</summary>
  public static DateOnly Start(DateOnly today, double workingDays) =>
    Finish(today, Math.Floor(Math.Max(0, workingDays) + 1e-9) + 1);

  /// <summary>The date a stretch of <paramref name="workingDays"/> started on <paramref name="today"/> ends.</summary>
  public static DateOnly Finish(DateOnly today, double workingDays)
  {
    var start = FirstOnOrAfter(today);
    // Rounding up a sum of durations should not cost a day for its last bit.
    var daysAfterStart = workingDays <= 0 ? 0 : (int)Math.Ceiling(workingDays - 1e-9) - 1;
    var day = start.AddDays(daysAfterStart / 5 * 7);

    for (var remaining = daysAfterStart % 5; remaining > 0; remaining--)
    {
      day = FirstOnOrAfter(day.AddDays(1));
    }

    return day;
  }
}
