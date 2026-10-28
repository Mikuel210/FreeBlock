using Core;

namespace Daemon;

public static class NotificationManager
{

    public static void Update()
    {
        foreach (var schedule in State.Schedules)
        {
            if (schedule.Active) continue;
            if (!schedule.InWarningPeriod) continue;

            // The most recent occurrence of the (possibly wrapped) warning start at or before now
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

    public static void Notify(string title, string body)
    {
        foreach (var user in Platform.GetCurrentUsers())
            Platform.SendNotification(user, title, body);
    }

}
