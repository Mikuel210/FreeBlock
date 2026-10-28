namespace Daemon;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        State.Initialize();
        Config.Initialize();

        while (!token.IsCancellationRequested)
        {
            await Blocker.UpdateAsync();
            State.Update();
            NotificationManager.Update();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Updated block: {time}", DateTimeOffset.Now);

            await Task.Delay(1000, token);
        }
    }
}
