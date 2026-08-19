using Core;

namespace Daemon;

public static class NotificationManager
{

    public static void Update()
    {
        foreach (var schedule in State.Schedules)
        {
            Console.WriteLine(schedule.Options.WarningTime);
            var startTime = schedule.StartTime.Add(-schedule.Options.WarningTime);
            var endTime = startTime.Add(TimeSpan.FromSeconds(1));

            if (!Schedule.IsActive(startTime, schedule.EndTime, schedule.Days) || Schedule.IsActive(endTime, schedule.EndTime, schedule.Days)) continue;

            string title = schedule.Options.WarningTime == TimeSpan.Zero ?
                $"Schedule started: {schedule.Name}" :
                $"Schedule starting soon: {schedule.Name}";

            string body = schedule.Options.WarningTime == TimeSpan.Zero ?
                "All browsers and all blocked apps have closed" :
                $"All browsers and all blocked apps will close in {schedule.Options.WarningTime.ToNaturalLanguage()}";

            Notify(title, body);
        }
    }

    private static void Notify(string title, string body)
    {
        foreach (var user in Platform.GetCurrentUsers())
            Platform.SendNotification(user, title, body);
    }

}
