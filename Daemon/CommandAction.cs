using System.Diagnostics;
using System.Text;

namespace Daemon;

public record CommandAction(List<KeyValuePair<string, string>> Commands, Dictionary<string, string>? Environment = null)
{

    /// <summary>
    /// Runs every command in sequence, waiting for each to exit. Returns the concatenated output and
    /// the exit codes, so a failure is visible to the caller instead of silently discarded (D7).
    /// </summary>
    public async Task<(int[] ExitCodes, string Output)> Run(params object?[] args)
    {
        int[] exitCodes = new int[Commands.Count];
        StringBuilder output = new();

        for (int i = 0; i < Commands.Count; i++)
        {
            var commandArguments = Commands[i];
            var command = string.Format(commandArguments.Key, args);
            var arguments = string.Format(commandArguments.Value, args);

            var (exitCode, stdout) = await RunProcess(command, arguments, Environment);
            exitCodes[i] = exitCode;

            if (stdout.Length > 0)
            {
                output.AppendLine(stdout);
                output.AppendLine();
            }

            if (exitCode != 0)
                output.AppendLine($"[FreeBlock] '{command}' exited with code {exitCode}");
        }

        return (exitCodes, output.ToString());
    }

    private static async Task<(int ExitCode, string Output)> RunProcess(string command, string arguments, Dictionary<string, string>? environment)
    {
        try
        {
            using var process = new Process();

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;

            if (environment is not null)
            {
                foreach (var entry in environment)
                    process.StartInfo.Environment[entry.Key] = entry.Value;
            }

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            string output = stdout;

            if (stderr.Length > 0)
            {
                if (output.Length > 0) output += System.Environment.NewLine;
                output += stderr;
            }

            return (process.ExitCode, output);
        }
        catch (Exception e)
        {
            return (-1, $"Failed to run '{command} {arguments}': {e.Message}");
        }
    }

}
