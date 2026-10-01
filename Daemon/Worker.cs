namespace Daemon;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        // Initialize
        State.Initialize();
        Config.Initialize();

        while (!token.IsCancellationRequested)
        {
            // B10: Apply current state before pruning, so expired locks are enforced until their last second
            await Blocker.UpdateAsync();
            State.Update();
            NotificationManager.Update();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Updated block: {time}", DateTimeOffset.Now);

            await Task.Delay(1000, token);
        }
    }
}
