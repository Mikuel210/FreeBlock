using System.Diagnostics;

namespace Daemon;

public record CommandAction(Dictionary<string, string> Commands, Dictionary<string, string>? Environment = null)
{

    public async Task Run(params object?[] args)
    {
        foreach (var commandArguments in Commands)
        {
            var command = string.Format(commandArguments.Key, args);
            var arguments = string.Format(commandArguments.Value, args);

            await Task.Run(() =>
            {
                var process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.FileName = command;
                process.StartInfo.Arguments = arguments;

                if (Environment != null)
                {
                    foreach (var entry in Environment)
                        process.StartInfo.Environment[entry.Key] = entry.Value;
                }

                process.Start();
            });
        }
    }

}
