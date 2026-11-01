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
    public bool Active => IsActive(StartTime, EndTime, Days, DateTime.Now);
    public bool InWarningPeriod => IsInWarningPeriod(StartTime, Days, DateTime.Now, Options.WarningTime);
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
        if (warningPeriod <= TimeSpan.Zero || warningPeriod > TimeSpan.FromHours(24)) return false;

        var nextStart = GetNextStart(startTime, days, now);
        return nextStart.HasValue && nextStart.Value - now <= warningPeriod;
    }

    public static DateTime? GetNextStart(TimeOnly startTime, DayOfWeek[] days, DateTime now)
    {
        // Find the next selected day
        for (var offset = 0; offset <= 7; offset++)
        {
            if (now.Date > DateTime.MaxValue.Date.AddDays(-offset)) break;

            var date = now.Date.AddDays(offset);
            if (!days.Contains(date.DayOfWeek)) continue;

            var candidate = date.Add(startTime.ToTimeSpan());
            if (candidate > now) return candidate;
        }

        return null;
    }

    public static bool PreservesProtectedWindow(Schedule original, Schedule replacement, DateTime now)
    {
        // Keep protected entries
        if ((original.ActiveAt(now) || original.InWarningPeriodAt(now))
            && original.Entries.Any(entry => !replacement.Entries.Contains(entry)))
        {
            return false;
        }

        // Preserve the active window
        if (original.ActiveAt(now))
        {
            var date = now.Date;

            if (original.EndTime < original.StartTime && now.TimeOfDay < original.StartTime.ToTimeSpan())
            {
                if (date == DateTime.MinValue.Date) return false;
                date = date.AddDays(-1);
            }

            if (!Covers(date.Add(original.StartTime.ToTimeSpan()))) return false;
        }

        // Preserve the warned window
        if (original.InWarningPeriodAt(now))
        {
            var next = GetNextStart(original.StartTime, original.Days, now);
            if (next.HasValue && !Covers(next.Value)) return false;
        }

        return true;

        bool Covers(DateTime start)
        {
            // Find a replacement covering the full window
            if (DateTime.MaxValue - start < original.Duration) return false;

            var end = start.Add(original.Duration);

            // Every nonempty daily window is shorter than 24h; only these two starts can cover it
            for (var offset = 0; offset >= -1; offset--)
            {
                if (offset == -1 && start.Date == DateTime.MinValue.Date) continue;

                var date = start.Date.AddDays(offset);
                if (!replacement.Days.Contains(date.DayOfWeek)) continue;

                var candidate = date.Add(replacement.StartTime.ToTimeSpan());

                if (candidate <= start && DateTime.MaxValue - candidate >= replacement.Duration
                    && candidate.Add(replacement.Duration) >= end)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static bool PreservesProtectedWindows(Schedule original, TimeOnly start, TimeOnly end, DayOfWeek[] days, DateTime now)
    {
        // Find active and warned windows
        for (int offset = -1; offset <= 1; offset++)
        {
            var date = now.Date.AddDays(offset);
            if (!original.Days.Contains(date.DayOfWeek)) continue;

            var oldStart = date.Add(original.StartTime.ToTimeSpan());
            var oldEnd = oldStart.Add(original.Duration);
            bool active = oldStart <= now && now < oldEnd;
            bool warned = now < oldStart && oldStart - now <= original.Options.WarningTime;

            if (!active && !warned) continue;

            // Check replacement coverage
            bool covered = false;

            for (int candidateOffset = -1; candidateOffset <= 1; candidateOffset++)
            {
                var candidateDate = date.AddDays(candidateOffset);
                if (!days.Contains(candidateDate.DayOfWeek)) continue;

                var newStart = candidateDate.Add(start.ToTimeSpan());
                var newEnd = newStart.Add(GetDuration(start, end));
                if (newStart <= oldStart && newEnd >= oldEnd) covered = true;
            }

            if (!covered) return false;
        }

        return true;
    }

    public bool ActiveAt(DateTime now) => IsActive(StartTime, EndTime, Days, now);
    public bool InWarningPeriodAt(DateTime now) => IsInWarningPeriod(StartTime, Days, now, Options.WarningTime);

    public static bool IsActive(TimeOnly startTime, TimeOnly endTime, DayOfWeek[] days)
        => IsActive(startTime, endTime, days, DateTime.Now);

    public void PrintStatus()
    {
        string timeString = $"({StartTime} - {EndTime}, {Days.GetDaysString()})";
        string icon = Active ? "🟢" : (InWarningPeriod ? "🟠" : "🔴");
        Console.WriteLine($"⏰{icon} {Name} {timeString}");
    }

}
