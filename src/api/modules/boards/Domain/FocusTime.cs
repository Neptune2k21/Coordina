namespace Coordina.Api.Modules.Boards.Domain;

/// <summary>
/// The focus hours that fit in a stretch of time, for a person who gives six
/// of them to cards on a working day. A stretch within one day counts its
/// hours, up to six; a longer one counts each working day by the share of it
/// that is inside, so a night or a weekend in the middle adds nothing unfair.
/// </summary>
public static class FocusTime
{
  private static readonly TimeSpan Day = TimeSpan.FromDays(1);

  public static double Between(DateTimeOffset start, DateTimeOffset end)
  {
    if (end <= start)
    {
      return 0;
    }

    var from = start.UtcDateTime;
    var to = end.UtcDateTime;

    if (from.Date == to.Date)
    {
      return Math.Min((to - from).TotalHours, WorkingDays.FocusHoursPerDay);
    }

    var days = 0.0;

    for (var day = from.Date; day < to; day += Day)
    {
      if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
      {
        continue;
      }

      var dayStart = day < from ? from : day;
      var dayEnd = day + Day > to ? to : day + Day;
      days += (dayEnd - dayStart) / Day;
    }

    return days * WorkingDays.FocusHoursPerDay;
  }
}
