namespace Daemon;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        // Initialize
        Config.Initialize();

        while (!token.IsCancellationRequested)
        {
            State.Update();
            await NotificationManager.UpdateAsync();
            await Blocker.UpdateAsync();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Updated block: {time}", DateTimeOffset.Now);

            await Task.Delay(1000, token);
        }
    }
}
