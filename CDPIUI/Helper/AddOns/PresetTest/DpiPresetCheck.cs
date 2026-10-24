using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// The DPI checker test: posts a random payload to every endpoint of the online suite over each
/// protocol variant. An upload that gets no answer before the timeout means the connection was
/// frozen by DPI.
/// </summary>
internal sealed class DpiPresetCheck : IPresetCheck
{
    private const int DpiRangeBytes = 65536;

    private static readonly Regex DpiMetricsRegex = new(
        @"^(?<code>\d{3})\s+(?<up>\d+)\s+(?<down>\d+)\s+(?<time>[\d\.]+)$",
        RegexOptions.Compiled);

    private readonly CurlProcessRunner _curl;
    private readonly SemaphoreSlim _gate;
    private readonly List<DpiSuiteEntry> _targets;
    private readonly string _payloadFile;

    public DpiPresetCheck(CurlProcessRunner curl, SemaphoreSlim gate, List<DpiSuiteEntry> targets)
    {
        _curl = curl;
        _gate = gate;
        _targets = targets;
        _payloadFile = CreatePayloadFile();
    }

    private enum DpiStatus { Ok, Fail, Unsupported, Blocked }

    public async Task RunAsync(PresetTestResult result, PresetTestLog log, CancellationToken ct)
    {
        string rangeSpec = $"0-{DpiRangeBytes - 1}";

        bool warnDetected = false;
        DpiOutcome[] outcomes = await Task.WhenAll(_targets.Select(target => CheckTargetAsync(target, rangeSpec, ct)));
        foreach (DpiOutcome outcome in outcomes)
        {
            ct.ThrowIfCancellationRequested();

            WriteOutcome(outcome, result, log);
            if (outcome.Warned)
                warnDetected = true;
        }

        log.Line(new TestLogSegment(""));
        if (warnDetected)
            log.Tagged("PT_Warn", TestLogColor.Red, PresetTestLog.Text("PT_DpiBlockedWarn"));
        else
            log.Tagged("PT_Ok", TestLogColor.Green, PresetTestLog.Text("PT_DpiOkAll"));
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_payloadFile);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't delete payload file: {ex.Message}");
        }
    }

    private static void WriteOutcome(DpiOutcome outcome, PresetTestResult result, PresetTestLog log)
    {
        log.Line(new TestLogSegment($"  === [{outcome.Country}][{outcome.Provider}] {outcome.Id} ===", TestLogColor.Cyan));
        foreach (DpiLine dl in outcome.Lines)
        {
            log.Line(
                new TestLogSegment("    " + dl.Label.PadRight(8) + " ", TestLogColor.Gray),
                new TestLogSegment($"code={dl.Code} ", TestLogColor.Gray),
                new TestLogSegment($"up={dl.UpKb:0.0}KB down={dl.DownKb:0.0}KB time={dl.Time:0.00}s ", TestLogColor.Gray),
                new TestLogSegment(dl.StatusText, dl.Color));

            switch (dl.Kind)
            {
                case DpiStatus.Ok:
                    result.DpiOk++;
                    break;
                case DpiStatus.Unsupported:
                    result.DpiUnsupported++;
                    break;
                case DpiStatus.Blocked:
                    result.DpiBlocked++;
                    break;
                default:
                    result.DpiFail++;
                    break;
            }
        }
        if (outcome.Warned)
            log.Line(new TestLogSegment("    " + PresetTestLog.Text("PT_DpiFreezeNote"), TestLogColor.Yellow));
        else
            log.Line(new TestLogSegment("    " + PresetTestLog.Text("PT_DpiNoFreeze"), TestLogColor.Green));
    }

    private async Task<DpiOutcome> CheckTargetAsync(DpiSuiteEntry target, string rangeSpec, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var outcome = new DpiOutcome { Id = target.id, Provider = target.provider, Country = target.country, Lines = new List<DpiLine>() };

            foreach (var test in CurlProcessRunner.ProtocolVariants)
            {
                var argList = new List<string>
                {
                    "--range", rangeSpec,
                    "-m", CurlProcessRunner.TimeoutSeconds.ToString(),
                    "-w", "%{http_code} %{size_upload} %{size_download} %{time_total}",
                    "-o", "NUL",
                    "-X", "POST",
                    "--data-binary", "@" + _payloadFile,
                    "-s"
                };
                argList.AddRange(test.Args);
                argList.Add($"https://{target.host}");

                var (exit, stdout, stderr) = await _curl.RunAsync(argList, ct);
                DpiLine line = ParseLine(test.Label, exit, stdout, stderr);
                outcome.Lines.Add(line);
                if (line.Kind == DpiStatus.Blocked)
                    outcome.Warned = true;
            }

            return outcome;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static DpiLine ParseLine(string label, int exit, string stdout, string stderr)
    {
        string text = (stdout ?? string.Empty).Trim();

        string code = "NA";
        long up = 0, down = 0;
        double time = -1;

        Match m = DpiMetricsRegex.Match(text);
        if (m.Success)
        {
            code = m.Groups["code"].Value;
            up = long.Parse(m.Groups["up"].Value);
            down = long.Parse(m.Groups["down"].Value);
            time = double.Parse(m.Groups["time"].Value, CultureInfo.InvariantCulture);
        }
        else if (CurlErrorPatterns.Unsupported.IsMatch((stderr ?? string.Empty) + " " + text))
        {
            code = "UNSUP";
        }
        else if (text.Length > 0)
        {
            code = "ERR";
        }

        DpiStatus kind;
        if (code == "UNSUP")
            kind = DpiStatus.Unsupported;
        else if (exit != 0 || code == "ERR" || code == "NA")
            kind = DpiStatus.Fail;
        else
            kind = DpiStatus.Ok;

        if (up > 0 && down == 0 && time >= CurlProcessRunner.TimeoutSeconds && exit != 0)
            kind = DpiStatus.Blocked;

        return MakeDpiLine(label, code, up, down, time, kind);
    }

    private static DpiLine MakeDpiLine(string label, string code, long up, long down, double time, DpiStatus kind)
    {
        (string text, TestLogColor color) = kind switch
        {
            DpiStatus.Ok => (PresetTestLog.Text("PT_StatusOk"), TestLogColor.Green),
            DpiStatus.Unsupported => (PresetTestLog.Text("PT_StatusUnsupported"), TestLogColor.Yellow),
            DpiStatus.Blocked => (PresetTestLog.Text("PT_StatusBlocked"), TestLogColor.Yellow),
            _ => (PresetTestLog.Text("PT_StatusFail"), TestLogColor.Red),
        };

        return new DpiLine
        {
            Label = label,
            Code = code,
            UpKb = Math.Round(up / 1024.0, 1),
            DownKb = Math.Round(down / 1024.0, 1),
            Time = time,
            Kind = kind,
            StatusText = text,
            Color = color
        };
    }

    private static string CreatePayloadFile()
    {
        string file = Path.GetTempFileName();
        var payload = new byte[DpiRangeBytes];
        RandomNumberGenerator.Fill(payload);
        File.WriteAllBytes(file, payload);
        return file;
    }

    private sealed class DpiLine
    {
        public string Label;
        public string Code;
        public double UpKb;
        public double DownKb;
        public double Time;
        public DpiStatus Kind;
        public string StatusText;
        public TestLogColor Color;
    }

    private sealed class DpiOutcome
    {
        public string Id;
        public string Provider;
        public string Country;
        public List<DpiLine> Lines;
        public bool Warned;
    }
}
