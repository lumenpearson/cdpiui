using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using CDPIUI.Shared.Security;

internal static class Program
{
    private static readonly (string Name, string RelativePath)[] Tools =
    [
        ("Update", Path.Combine("Update", "Update.exe")),
        ("EditHostFile", Path.Combine("EditHostFile", "EditHostFile.exe")),
        ("CDPIUI_ElevatedLauncher", Path.Combine("launcher", "CDPIUI_ElevatedLauncher.exe")),
    ];

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: ToolsSmokeTest.exe <tools-directory>");
            return 2;
        }

        string toolsDirectory;
        try
        {
            toolsDirectory = Path.GetFullPath(args[0]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Invalid tools directory: {ex.Message}");
            return 2;
        }

        int failures = 0;
        var report = new List<string>();
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            {
                const string message = "[FAIL] ToolsSmokeTest: runner is not elevated.";
                AddReport(report, message);
                string failedReportPath = Path.Combine(
                    Path.GetDirectoryName(Environment.ProcessPath)!, "ToolsSmokeTest.report.txt");
                File.WriteAllLines(failedReportPath, report);
                return 3;
            }
        }
        AddReport(report, "[PASS] ToolsSmokeTest: runner has an administrator token.");

        string probeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            "CDPIUI-Staging.Smoke-" + Guid.NewGuid().ToString("N"));
        string probeSource = Path.Combine(toolsDirectory, "smoke-source-" + Guid.NewGuid().ToString("N") + ".bin");
        string? probePackage = null;
        try
        {
            File.WriteAllText(probeSource, "staging smoke bytes");
            probePackage = ProtectedStagingArea.Stage(probeRoot, probeSource);
            ProtectedStagingArea.RequireProtectedTree(probeRoot);
            using (ReadOnlyPathLease lease = ReadOnlyPathLease.OpenFile(probePackage))
            {
                using var reader = new StreamReader(lease.Stream!);
                if (reader.ReadToEnd() != "staging smoke bytes")
                    throw new InvalidDataException("Staged contents changed.");
            }
            AddReport(report, "[PASS] Protected staging: private owner/DACL, source copy and read lease.");
        }
        catch (Exception ex)
        {
            AddReport(report, $"[FAIL] Protected staging: {ex}");
            failures++;
        }
        finally
        {
            try
            {
                if (probePackage != null) ProtectedStagingArea.Release(probeRoot, probePackage);
                if (Directory.Exists(probeRoot))
                {
                    ProtectedStagingArea.RequireProtectedPath(probeRoot);
                    Directory.Delete(probeRoot, recursive: false);
                }
                File.Delete(probeSource);
            }
            catch (Exception ex) { AddReport(report, $"[FAIL] Staging probe cleanup: {ex.Message}"); failures++; }
        }

        foreach ((string name, string relativePath) in Tools)
        {
            string executable = Path.Combine(toolsDirectory, relativePath);
            if (!File.Exists(executable))
            {
                AddReport(report, $"[FAIL] {name}: not found: {executable}");
                failures++;
                continue;
            }

            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo(executable)
                    {
                        WorkingDirectory = Path.GetDirectoryName(executable)!,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                };
                process.StartInfo.ArgumentList.Add("--smoke-test");

                if (!process.Start())
                    throw new InvalidOperationException("Process.Start returned false.");

                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                Task exit = process.WaitForExitAsync();
                if (await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(20))) != exit)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                    AddReport(report, $"[FAIL] {name}: timed out after 20 seconds.");
                    failures++;
                    continue;
                }

                string output = await stdout;
                string error = await stderr;
                if (process.ExitCode == 0)
                {
                    AddReport(report, $"[PASS] {name}: launched and exited with code 0.");
                }
                else
                {
                    AddReport(report, $"[FAIL] {name}: exit code {process.ExitCode}.");
                    if (!string.IsNullOrWhiteSpace(output))
                        AddReport(report, output.TrimEnd());
                    if (!string.IsNullOrWhiteSpace(error))
                        AddReport(report, error.TrimEnd());
                    failures++;
                }
            }
            catch (Exception ex)
            {
                AddReport(report, $"[FAIL] {name}: {ex}");
                failures++;
            }
        }

        AddReport(report, failures == 0
            ? "All tool smoke tests passed."
            : $"{failures} tool smoke test(s) failed.");

        string reportPath = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath)!, "ToolsSmokeTest.report.txt");
        File.WriteAllLines(reportPath, report);
        return failures == 0 ? 0 : 1;
    }

    private static void AddReport(List<string> report, string message)
    {
        report.Add(message);
        Console.WriteLine(message);
    }
}
