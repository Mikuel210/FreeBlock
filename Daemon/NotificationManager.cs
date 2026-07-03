using SDK;

namespace Daemon;

public static class NotificationManager
{

    public static void Update()
    {
        foreach (var schedule in State.Schedules)
        {
            var startTime = schedule.StartTime.AddMinutes(-1); // TODO: Config
            var endTime = startTime.Add(TimeSpan.FromSeconds(1));

            if (!Schedule.IsActive(startTime, schedule.EndTime, schedule.Days) || Schedule.IsActive(endTime, schedule.EndTime, schedule.Days)) continue;
            Notify($"Schedule starting soon: {schedule.Name}", "All browsers and all blocked apps will close in 1 minute");
        }
    }

    private static void Notify(string title, string body)
    {
        foreach (var user in Platform.GetCurrentUsers())
            Platform.SendNotification(user, title, body);
    }

}
