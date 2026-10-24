using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.PresetTest;

namespace CDPIUI.Helper.AddOns;

/// <summary>
/// Runs every preset of a component in turn and checks how well each one works, in the standard
/// mode (endpoints of test_targets.txt) or the DPI checker mode (an online suite).
/// </summary>
public class PresetTestHelper
{
    private const int StartWaitMs = 5000;
    private const int StopWaitMs = 1500;
    private const int MaxParallel = 8;

    private readonly CurlProcessRunner _curl = new();

    public static string GetTargetsFilePath() => PresetTestTargetsFile.GetPath();

    public static void EnsureTargetsFile() => PresetTestTargetsFile.EnsureExists();

    public static List<PresetTestTarget> LoadTargets() => PresetTestTargetsFile.Load();

    public void CancelAllCurls() => _curl.CancelAll();

    public static PresetTestRunResult BuildRunResult(List<PresetTestResult> results, PresetTestType testType, bool wasCancelled)
    {
        var ranked = results
            .OrderByDescending(r => r.GetScore(testType))
            .ThenByDescending(r => r.PingOk)
            .ToList();

        // A preset without a single successful check is no recommendation.
        PresetTestResult best = ranked.FirstOrDefault(r => !r.Failed &&
            (testType == PresetTestType.Standard ? r.HttpOk > 0 : r.DpiOk > 0));

        return new PresetTestRunResult
        {
            Ranked = ranked,
            Best = best,
            WasCancelled = wasCancelled
        };
    }

    public static string GetPackDisplayName(ConfigItem preset)
    {
        if (preset == null || string.IsNullOrEmpty(preset.packId))
            return string.Empty;
        try
        {
            var item = DatabaseHelper.Instance.GetItemById(preset.packId);
            if (item != null && !string.IsNullOrEmpty(item.ShortName))
                return item.ShortName;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't get the name of pack {preset.packId}: {ex.Message}");
        }
        return preset.packId;
    }

    public async Task<PresetTestRunResult> RunAsync(
        string componentId,
        ProcessService processManager,
        PresetTestType testType,
        List<ConfigItem> presets,
        IProgress<List<TestLogSegment>> log,
        IProgress<PresetTestProgress> progress,
        CancellationToken ct)
    {
        var results = new List<PresetTestResult>();
        var testLog = new PresetTestLog(log);
        var gate = new SemaphoreSlim(MaxParallel);
        bool wasCancelled = false;
        var runStopwatch = Stopwatch.StartNew();

        if (!CurlProcessRunner.IsAvailable())
        {
            testLog.Tagged("PT_Warn", TestLogColor.Red, PresetTestLog.Text("PT_NoCurl"));
            return BuildRunResult(results, testType, false);
        }
        testLog.Tagged("PT_Ok", TestLogColor.Green, PresetTestLog.Text("PT_CurlFound"));

        if (presets == null || presets.Count == 0)
        {
            testLog.Tagged("PT_Warn", TestLogColor.Yellow, PresetTestLog.Text("PT_NoPresets"));
            return BuildRunResult(results, testType, false);
        }

        IPresetCheck check = await CreateCheckAsync(testType, gate, testLog, ct);
        if (check == null)
        {
            return BuildRunResult(results, testType, false);
        }

        WriteHeader(testLog, testType, presets.Count);

        // The test restarts the component with every preset; afterwards it runs again with the
        // user's own preset if it was running before the test.
        bool wasRunning = processManager.IsProcessRunning;
        try
        {
            int index = 0;
            foreach (ConfigItem preset in presets)
            {
                ct.ThrowIfCancellationRequested();
                index++;

                string presetName = string.IsNullOrEmpty(preset.name) ? preset.file_name ?? $"#{index}" : preset.name;
                ReportProgress(progress, index - 1, presets.Count, presetName, runStopwatch);

                PresetTestResult result = await TestPresetAsync(
                    componentId, processManager, preset, presetName, index, presets.Count, check, testLog, ct);
                results.Add(result);
                if (!result.Failed)
                    ReportProgress(progress, index, presets.Count, presetName, runStopwatch);
            }

            await processManager.StopProcess(false);

            PresetTestSummary.Write(results, testType, testLog);
            PresetTestSummary.SaveToFile(componentId, testType, results, testLog);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            CancelAllCurls();
        }
        finally
        {
            CancelAllCurls();
            check.Dispose();
            // Whatever ended the test, the component must not keep running with a tested preset.
            try
            {
                await processManager.StopProcess(false);
                if (wasRunning)
                    await processManager.StartProcess();
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't restore the component state after the test: {ex.Message}");
            }
        }

        return BuildRunResult(results, testType, wasCancelled);
    }

    /// <summary>
    /// Loads the endpoints of <paramref name="testType"/>. Returns <see langword="null"/> when
    /// there is nothing to check.
    /// </summary>
    private async Task<IPresetCheck> CreateCheckAsync(
        PresetTestType testType,
        SemaphoreSlim gate,
        PresetTestLog log,
        CancellationToken ct)
    {
        if (testType == PresetTestType.Standard)
        {
            List<PresetTestTarget> targets = LoadTargets();
            if (targets.Count == 0)
            {
                log.Tagged("PT_Warn", TestLogColor.Yellow, PresetTestLog.Text("PT_NoTargets"));
                return null;
            }
            int maxNameLen = Math.Max(10, targets.Max(t => t.Name.Length));
            log.Tagged("PT_Info", TestLogColor.Cyan, string.Format(PresetTestLog.Text("PT_TargetsLoaded"), targets.Count));
            return new StandardPresetCheck(_curl, gate, targets, maxNameLen);
        }

        List<DpiSuiteEntry> dpiTargets = await DpiSuiteSource.FetchAsync(ct);
        if (dpiTargets == null || dpiTargets.Count == 0)
        {
            log.Tagged("PT_Warn", TestLogColor.Yellow, PresetTestLog.Text("PT_DpiSuiteFailed"));
            return null;
        }
        log.Tagged("PT_Info", TestLogColor.Cyan, string.Format(PresetTestLog.Text("PT_DpiTargetsLoaded"), dpiTargets.Count));
        return new DpiPresetCheck(_curl, gate, dpiTargets);
    }

    private static void WriteHeader(PresetTestLog log, PresetTestType testType, int presetCount)
    {
        log.Line(new TestLogSegment(""));
        log.Line(new TestLogSegment(new string('=', 60), TestLogColor.Gray));
        log.Line(new TestLogSegment("            " + PresetTestLog.Text("PT_Header"), TestLogColor.White));
        log.Line(new TestLogSegment("            " + PresetTestLog.Text("PT_ModeLabel") + " " +
            (testType == PresetTestType.Standard ? PresetTestLog.Text("PT_ModeStandard") : PresetTestLog.Text("PT_ModeDpi")), TestLogColor.White));
        log.Line(new TestLogSegment("            " + string.Format(PresetTestLog.Text("PT_TotalConfigs"), presetCount), TestLogColor.White));
        log.Line(new TestLogSegment(new string('=', 60), TestLogColor.Gray));
        log.Tagged("PT_Warn", TestLogColor.Yellow, PresetTestLog.Text("PT_MayTakeTime"));
    }

    /// <summary>
    /// Starts the component with <paramref name="preset"/> and runs the checks. A preset that has
    /// no arguments or does not start is returned as failed.
    /// </summary>
    private static async Task<PresetTestResult> TestPresetAsync(
        string componentId,
        ProcessService processManager,
        ConfigItem preset,
        string presetName,
        int index,
        int presetCount,
        IPresetCheck check,
        PresetTestLog log,
        CancellationToken ct)
    {
        log.Line(new TestLogSegment(""));
        log.Line(new TestLogSegment(new string('-', 60), TestLogColor.Gray));
        log.Line(new TestLogSegment("  " + string.Format(PresetTestLog.Text("PT_Preset"), index, presetCount, presetName), TestLogColor.White));
        log.Line(new TestLogSegment(new string('-', 60), TestLogColor.Gray));

        var result = new PresetTestResult
        {
            Preset = preset,
            PresetName = presetName,
            PackName = GetPackDisplayName(preset)
        };

        await processManager.StopProcess(false);
        await SafeDelay(StopWaitMs, ct);

        string args = ConfigurationService.GetStartupParametersByConfigItem(preset);
        if (string.IsNullOrWhiteSpace(args))
        {
            log.Line(new TestLogSegment("  > ", TestLogColor.Gray), new TestLogSegment(PresetTestLog.Text("PT_Skipped"), TestLogColor.Yellow));
            result.Failed = true;
            return result;
        }

        log.Line(new TestLogSegment("  > " + PresetTestLog.Text("PT_Starting"), TestLogColor.Cyan));
        // The task of this component owns processManager, so the task manager starts the
        // same process with the preset's arguments.
        await ComponentTasksManager.Instance.CreateAndRunNewTask(componentId, args);
        await SafeDelay(StartWaitMs, ct);

        if (!processManager.IsProcessRunning || processManager.IsErrorHappens)
        {
            log.Line(new TestLogSegment("  ", TestLogColor.Gray), new TestLogSegment(PresetTestLog.Text("PT_FailedToStart"), TestLogColor.Red));
            result.Failed = true;
            return result;
        }

        log.Line(new TestLogSegment("  > " + PresetTestLog.Text("PT_RunningTests"), TestLogColor.Gray));
        await check.RunAsync(result, log, ct);
        return result;
    }

    private static void ReportProgress(IProgress<PresetTestProgress> progress, int completed, int total, string currentName, Stopwatch sw)
    {
        if (progress == null || total <= 0)
            return;

        TimeSpan? eta = null;
        if (completed > 0 && completed < total)
        {
            double avgMs = sw.Elapsed.TotalMilliseconds / completed;
            eta = TimeSpan.FromMilliseconds(avgMs * (total - completed));
        }

        progress.Report(new PresetTestProgress
        {
            CompletedPresets = completed,
            TotalPresets = total,
            CurrentIndex = Math.Min(completed + 1, total),
            CurrentPresetName = currentName ?? string.Empty,
            Percent = total > 0 ? 100.0 * completed / total : 0,
            EstimatedRemaining = eta
        });
    }

    private static async Task SafeDelay(int ms, CancellationToken ct)
    {
        try
        { await Task.Delay(ms, ct); }
        catch (TaskCanceledException) { throw new OperationCanceledException(ct); }
    }
}
