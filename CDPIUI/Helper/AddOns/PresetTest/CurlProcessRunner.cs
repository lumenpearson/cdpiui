using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// Runs curl.exe for the preset test and keeps track of the running processes, so that
/// a stopped test can kill all of them at once.
/// </summary>
internal sealed class CurlProcessRunner
{
    public const int TimeoutSeconds = 5;

    /// <summary>
    /// The protocol variants each endpoint is checked with: a label and the curl options.
    /// </summary>
    public static readonly (string Label, string[] Args)[] ProtocolVariants =
    {
        ("HTTP", new[] { "--http1.1" }),
        ("TLS1.2", new[] { "--tlsv1.2", "--tls-max", "1.2" }),
        ("TLS1.3", new[] { "--tlsv1.3", "--tls-max", "1.3" }),
    };

    private readonly object _curlLock = new();
    private readonly List<Process> _activeCurls = new();

    public static bool IsAvailable()
    {
        try
        {
            string system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
            if (File.Exists(system32))
                return true;

            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;
                // A PATH entry can be malformed: skip it and check the next one.
                try
                { if (File.Exists(Path.Combine(dir.Trim(), "curl.exe"))) return true; }
                catch (ArgumentException) { }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't look for curl.exe: {ex.Message}");
        }
        return false;
    }

    public void CancelAll()
    {
        lock (_curlLock)
        {
            foreach (Process p in _activeCurls.ToList())
            {
                try
                {
                    if (p != null && !p.HasExited)
                        p.Kill(entireProcessTree: true);
                }
                // The process can exit between the check and Kill: nothing is left to stop.
                catch (Exception ex) { Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Cannot stop curl: {ex.Message}"); }
            }
            _activeCurls.Clear();
        }
    }

    public async Task<(int exit, string stdout, string stderr)> RunAsync(IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("curl.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string a in args)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        lock (_curlLock)
            _activeCurls.Add(process);
        try
        {
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                return (-1, string.Empty, ex.Message);
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);

            using (ct.Register(() => TryKill(process)))
            {
                try
                {
                    await process.WaitForExitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    throw;
                }
            }

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            return (process.ExitCode, stdout, stderr);
        }
        finally
        {
            lock (_curlLock)
                _activeCurls.Remove(process);
        }
    }

    private static void TryKill(Process process)
    {
        // The process can exit between the check and Kill: nothing is left to stop.
        try
        { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception ex) { Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Cannot stop curl: {ex.Message}"); }
    }
}
