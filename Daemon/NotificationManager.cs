using Core;

namespace Daemon;

public static class NotificationManager
{

    /// <summary>
    /// Fires once when the warning period for a schedule starts. Idempotent and restart-safe: the
    /// firing is recorded on the schedule, so a daemon restart inside the window, or a tick that took
    /// longer than the window, can neither re-fire nor skip the notification (B4). Gating on
    /// <see cref="Schedule.InWarningPeriod"/> keeps this correct for overnight warning periods too (B12).
    /// </summary>
    public static void Update()
    {
        foreach (var schedule in State.Schedules)
        {
            if (schedule.Active) continue;
            if (!schedule.InWarningPeriod) continue;

            // The most recent occurrence of the (possibly wrapped) warning start at or before now.
            var warningStart = schedule.StartTime.Add(-schedule.Options.WarningTime);
            var boundary = DateTime.Today.Add(warningStart.ToTimeSpan());
            if (boundary > DateTime.Now) boundary = boundary.AddDays(-1);

            if (schedule.LastNotificationTime is DateTime last && last >= boundary) continue;

            schedule.LastNotificationTime = DateTime.Now;

            string title = schedule.Options.WarningTime == TimeSpan.Zero ?
                $"Schedule started: {schedule.Name}" :
                $"Schedule starting soon: {schedule.Name}";

            string body = schedule.Options.WarningTime == TimeSpan.Zero ?
                "All browsers and all blocked apps have closed" :
                $"All browsers and all blocked apps will close in {schedule.Options.WarningTime.ToNaturalLanguage()}";

            Notify(title, body);
        }
    }

    // D12: Shared notification fan-out used by both NotificationManager and Blocker
    public static void Notify(string title, string body)
    {
        foreach (var user in Platform.GetCurrentUsers())
            Platform.SendNotification(user, title, body);
    }

}
