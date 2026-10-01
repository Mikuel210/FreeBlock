namespace Core;

public class Schedule : IAgent<ScheduleOptions>
{

    public string Name { get; set; } = string.Empty;
    public ScheduleOptions Options { get; set; } = new();

    // Properties
    public List<Entry> Entries { get; set; } = [];
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DayOfWeek[] Days { get; set; } = [];

    // Removal
    public DateTime? RemovalRequestTime { get; set; }

    // Notifications
    public DateTime? LastNotificationTime { get; set; }

    // Active

    /// <summary>
    /// Whether the schedule is currently blocking. <see cref="InWarningPeriod"/> shifts time
    /// <em>forward</em> by the warning time instead of shifting the start time back: <c>TimeOnly</c>
    /// arithmetic wraps modulo 24h, so subtracting a warning longer than the start time-of-day yields
    /// a start that refers to yesterday and the day-anchoring logic below silently skips the window (B12).
    /// Forward shifting keeps the arithmetic honest and needs no wrap handling at all.
    /// </summary>
    public bool Active => IsActive(StartTime, EndTime, Days, DateTime.Now);
    public bool InWarningPeriod => IsInWarningPeriod(StartTime, Days, DateTime.Now, Options.WarningTime);

    /// <summary>
    /// The length of the active window. A schedule whose end is at or before its start wraps overnight, so
    /// its window is (24h - start) + end, not end - start. Comparing times directly gets this backwards:
    /// moving 08:00-09:00 to 10:00-09:00 looks like a later start and an unchanged end, but it now runs 23h.
    /// </summary>
    public TimeSpan Duration => GetDuration(StartTime, EndTime);

    public static TimeSpan GetDuration(TimeOnly start, TimeOnly end)
    {
        var startTime = start.ToTimeSpan();
        var endTime = end.ToTimeSpan();

        return endTime >= startTime ? endTime - startTime : TimeSpan.FromHours(24) - startTime + endTime;
    }

    public static bool IsActive(TimeOnly startTime, TimeOnly endTime, DayOfWeek[] days, DateTime now)
    {
        bool isDay;

        // Check day
        if (endTime >= startTime || now.TimeOfDay >= startTime.ToTimeSpan()) isDay = days.Contains(now.DayOfWeek);
        else isDay = days.Contains(now.AddDays(-1).DayOfWeek);

        // Check time
        bool afterStartTime = now.TimeOfDay >= startTime.ToTimeSpan();
        bool beforeEndTime = now.TimeOfDay < endTime.ToTimeSpan();
        bool isTime = endTime >= startTime ? afterStartTime && beforeEndTime : afterStartTime || beforeEndTime;
        return isDay && isTime;
    }

    public static bool IsInWarningPeriod(TimeOnly startTime, DayOfWeek[] days, DateTime now, TimeSpan warningPeriod)
    {
        if (now.Add(warningPeriod).Date == now.Date)
        {
            return days.Contains(now.DayOfWeek)
                && startTime.ToTimeSpan() > now.TimeOfDay
                && (now + warningPeriod).TimeOfDay >= startTime.ToTimeSpan();
        }
        else
        {
            return days.Contains(now.AddDays(1).DayOfWeek)
                && (now + warningPeriod).TimeOfDay >= startTime.ToTimeSpan();
        }
    }

    /// <summary>Active/InWarningPeriod at an explicit moment, for tests and for any caller that wants
    /// a consistent snapshot instead of two reads of <see cref="DateTime.Now"/>.</summary>
    public bool ActiveAt(DateTime now) => IsActive(StartTime, EndTime, Days, now);
    public bool InWarningPeriodAt(DateTime now) => IsInWarningPeriod(StartTime, Days, now, Options.WarningTime);

    /// <summary>Kept for the CLI's eager checks; resolves to <see cref="DateTime.Now"/>.</summary>
    public static bool IsActive(TimeOnly startTime, TimeOnly endTime, DayOfWeek[] days)
        => IsActive(startTime, endTime, days, DateTime.Now);

    public void PrintStatus()
    {
        string timeString = $"({StartTime} - {EndTime}, {Days.GetDaysString()})";
        string icon = Active ? "🟢" : (InWarningPeriod ? "🟠" : "🔴");
        Console.WriteLine($"⏰{icon} {Name} {timeString}");
    }

}
