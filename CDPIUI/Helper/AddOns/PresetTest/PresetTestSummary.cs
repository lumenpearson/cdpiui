using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// The analytics at the end of a preset test: a table in the log and a text file in the logs folder.
/// </summary>
internal static class PresetTestSummary
{
    public static void Write(List<PresetTestResult> results, PresetTestType testType, PresetTestLog log)
    {
        log.Line(new TestLogSegment(""));
        log.Line(new TestLogSegment(new string('=', 60), TestLogColor.Gray));
        log.Line(new TestLogSegment("=== " + PresetTestLog.Text("PT_Analytics") + " ===", TestLogColor.White));
        log.Line(new TestLogSegment(new string('=', 60), TestLogColor.Gray));

        foreach (PresetTestResult r in results)
        {
            if (r.Failed)
            {
                log.Line(new TestLogSegment("  " + r.PresetName.PadRight(28) + " ", TestLogColor.White),
                    new TestLogSegment(PresetTestLog.Text("PT_FailedToStart"), TestLogColor.Red));
                continue;
            }

            log.Report(FormatResult(r, testType));
        }

        PresetTestResult best = FindBest(results, testType);

        log.Line(new TestLogSegment(""));
        bool hasWinner = best != null &&
            (testType == PresetTestType.Standard && (best.HttpOk > 0 || best.PingOk > 0) ||
             testType == PresetTestType.DpiChecker && best.DpiOk > 0);

        if (hasWinner)
            log.Line(new TestLogSegment(string.Format(PresetTestLog.Text("PT_BestConfig"), best.PresetName), TestLogColor.Green));
        else
            log.Line(new TestLogSegment(PresetTestLog.Text("PT_NoWorking"), TestLogColor.Yellow));
    }

    public static void SaveToFile(string componentId, PresetTestType testType, List<PresetTestResult> results, PresetTestLog log)
    {
        try
        {
            string logsDir = Path.Combine(Directories.DataDirectory, "Logs", "test results");
            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);

            string file = Path.Combine(logsDir, $"test_results_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"Preset test results for component {componentId} ({testType})");
            sb.AppendLine();
            foreach (PresetTestResult r in results)
            {
                if (r.Failed)
                    sb.AppendLine($"{r.PresetName} : did not start");
                else if (testType == PresetTestType.Standard)
                    sb.AppendLine($"{r.PresetName} : HTTP OK {r.HttpOk}, ERR {r.HttpErr}, UNSUP {r.Unsupported}, Ping OK {r.PingOk}, Fail {r.PingFail}");
                else
                    sb.AppendLine($"{r.PresetName} : OK {r.DpiOk}, FAIL {r.DpiFail}, UNSUP {r.DpiUnsupported}, BLOCKED {r.DpiBlocked}");
            }

            PresetTestResult best = FindBest(results, testType);
            sb.AppendLine();
            sb.AppendLine($"Best config: {(best != null ? best.PresetName : "none")}");

            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            log.Tagged("PT_Info", TestLogColor.Cyan, string.Format(PresetTestLog.Text("PT_ResultsSaved"), file));
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(PresetTestHelper), $"Can't save test results: {ex.Message}");
        }
    }

    private static List<TestLogSegment> FormatResult(PresetTestResult r, PresetTestType testType)
    {
        var segs = new List<TestLogSegment> { new TestLogSegment("  " + r.PresetName.PadRight(28) + "  ", TestLogColor.White) };
        if (testType == PresetTestType.Standard)
        {
            segs.Add(new TestLogSegment(PresetTestLog.Text("PT_AHttpOk") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.HttpOk.ToString(), TestLogColor.Green));
            segs.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_AErr") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.HttpErr.ToString(), r.HttpErr > 0 ? TestLogColor.Red : TestLogColor.Green));
            segs.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_AUnsupported") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.Unsupported.ToString(), TestLogColor.Yellow));
            segs.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_APingOk") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.PingOk.ToString(), TestLogColor.Cyan));
            segs.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_AFail") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.PingFail.ToString(), r.PingFail > 0 ? TestLogColor.Yellow : TestLogColor.Green));
        }
        else
        {
            segs.Add(new TestLogSegment(PresetTestLog.Text("PT_ADpiOk") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.DpiOk.ToString(), TestLogColor.Green));
            segs.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_ADpiFail") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.DpiFail.ToString(), r.DpiFail > 0 ? TestLogColor.Red : TestLogColor.Green));
            segs.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_AUnsupported") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.DpiUnsupported.ToString(), TestLogColor.Yellow));
            segs.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_ABlocked") + " ", TestLogColor.Gray));
            segs.Add(new TestLogSegment(r.DpiBlocked.ToString(), r.DpiBlocked > 0 ? TestLogColor.Yellow : TestLogColor.Green));
        }
        return segs;
    }

    private static PresetTestResult FindBest(List<PresetTestResult> results, PresetTestType testType) => results
        .Where(r => !r.Failed)
        .OrderByDescending(r => testType == PresetTestType.Standard ? r.HttpOk : r.DpiOk)
        .ThenByDescending(r => r.PingOk)
        .FirstOrDefault();
}
