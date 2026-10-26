using System.Diagnostics;
using CDPIUI.Core.Basic;

namespace CDPIUI.AddOns.Troubleshooting;

/// <summary>
/// Runs a system tool without a console window and without blocking the calling thread
/// </summary>
internal static class SystemToolRunner
{
    public static readonly string PowerShellPath =
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    public static async Task<(int ExitCode, string Output)> RunAsync(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Cannot start {fileName}");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            { process.Kill(); }
            catch (Exception ex)
            {
                // For example, the process exited at the same time; the timeout is reported below.
                Logger.Instance.CreateWarningLog(nameof(TroubleshootingService), $"Cannot stop {fileName}: {ex.Message}");
            }
            throw new System.TimeoutException($"{fileName} did not finish in time");
        }
        await errors;
        return (process.ExitCode, await output);
    }
}
