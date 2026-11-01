using Core;

namespace Daemon;

public static class NotificationManager
{
    public static void Update() => Update(DateTime.Now);

    public static void Update(DateTime now)
    {
        foreach (var schedule in State.Schedules)
        {
            // Find the occurrence to notify
            DateTime? occurrence = null;
            bool started = schedule.Options.WarningTime == TimeSpan.Zero;

            if (started)
            {
                if (!schedule.ActiveAt(now)) continue;

                var start = now.Date.Add(schedule.StartTime.ToTimeSpan());
                occurrence = start > now ? start.AddDays(-1) : start;
            }
            else
            {
                if (schedule.ActiveAt(now) || schedule.Options.WarningTime < TimeSpan.Zero) continue;

                // The next selected start is at most seven days away, regardless of warning length
                for (int offset = 0; offset <= 7; offset++)
                {
                    var candidate = now.Date.AddDays(offset).Add(schedule.StartTime.ToTimeSpan());

                    if (candidate <= now || !schedule.Days.Contains(candidate.DayOfWeek))
                        continue;

                    if (candidate - now <= schedule.Options.WarningTime)
                        occurrence = candidate;

                    break;
                }
            }

            if (occurrence is not DateTime startTime) continue;

            // Skip notifications already sent
            var boundary = started ? startTime : startTime.Subtract(schedule.Options.WarningTime);
            if (schedule.LastNotificationTime is DateTime last && last >= boundary) continue;

            // Send the notification
            string title = started ? $"Schedule started: {schedule.Name}" : $"Schedule starting soon: {schedule.Name}";
            string body = started ? "All browsers and all blocked apps have closed" :
                $"All browsers and all blocked apps will close in {(startTime - now).ToNaturalLanguage()}";

            if (!TryNotify(title, body)) continue;

            // Save delivery time
            var previous = schedule.LastNotificationTime;
            schedule.LastNotificationTime = now;

            try
            {
                State.Save();
            }
            catch (Exception e)
            {
                schedule.LastNotificationTime = previous;
                Console.Error.WriteLine($"Could not save notification timestamp: {e.Message}");
            }
        }
    }

    public static void Notify(string title, string body) => TryNotify(title, body);

    private static bool TryNotify(string title, string body)
    {
        bool sent = false;
        bool failed = false;

        // Notify each user independently
        try
        {
            foreach (var user in Platform.GetCurrentUsers())
            {
                try
                {
                    Platform.SendNotification(user, title, body);
                    sent = true;
                }
                catch (Exception e)
                {
                    failed = true;
                    Console.Error.WriteLine($"Notification failed for '{user}': {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Could not enumerate notification users: {e.Message}");
            return false;
        }

        return sent && !failed;
    }
}
