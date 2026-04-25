using SDK;

namespace Daemon;

public static class NotificationManager
{

    public static async Task UpdateAsync()
    {
        foreach (var schedule in State.Schedules)
        {
            var startTime = schedule.StartTime.AddMinutes(-1); // TODO: Config
            var endTime = startTime.Add(TimeSpan.FromSeconds(1));

            if (!Schedule.IsActive(startTime, schedule.EndTime, schedule.Days) || Schedule.IsActive(endTime, schedule.EndTime, schedule.Days)) continue;
            await NotifyAsync($"Schedule starting soon: {schedule.Name}", "All browsers will close in 1 minute");
        }
    }

    private static async Task NotifyAsync(string title, string body)
    {
        foreach (var user in Platform.GetCurrentUsers())
        {
            await Platform.SendNotification.Run(
                user,
                title,
                body
            );
        }
    }

}
