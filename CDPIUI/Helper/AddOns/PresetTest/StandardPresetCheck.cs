using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// The standard test: every endpoint of test_targets.txt over each protocol variant with curl,
/// and a ping of its host.
/// </summary>
internal sealed class StandardPresetCheck : IPresetCheck
{
    private readonly CurlProcessRunner _curl;
    private readonly SemaphoreSlim _gate;
    private readonly List<PresetTestTarget> _targets;
    private readonly int _maxNameLen;

    public StandardPresetCheck(
        CurlProcessRunner curl,
        SemaphoreSlim gate,
        List<PresetTestTarget> targets,
        int maxNameLen)
    {
        _curl = curl;
        _gate = gate;
        _targets = targets;
        _maxNameLen = maxNameLen;
    }

    private enum StdStatus { Ok, Error, Unsupported, Ssl }

    public async Task RunAsync(PresetTestResult result, PresetTestLog log, CancellationToken ct)
    {
        StandardOutcome[] outcomes = await Task.WhenAll(_targets.Select(target => CheckTargetAsync(target, ct)));
        foreach (StandardOutcome outcome in outcomes)
        {
            ct.ThrowIfCancellationRequested();

            List<TestLogSegment> segments = FormatOutcome(outcome);
            Count(outcome, result);
            log.Report(segments);
        }
    }

    public void Dispose()
    {
    }

    private List<TestLogSegment> FormatOutcome(StandardOutcome outcome)
    {
        var segments = new List<TestLogSegment>
        {
            new TestLogSegment("    " + outcome.Name.PadRight(_maxNameLen))
        };

        if (outcome.IsUrl)
        {
            foreach (StandardToken token in outcome.Tokens)
            {
                segments.Add(new TestLogSegment(" | " + token.Label + ":", TestLogColor.Gray));
                segments.Add(new TestLogSegment(token.StatusText, token.Color));
            }
            segments.Add(new TestLogSegment(" | " + PresetTestLog.Text("PT_ColPing") + ": ", TestLogColor.Gray));
            if (outcome.PingOk)
                segments.Add(new TestLogSegment(string.Format(PresetTestLog.Text("PT_Ms"), outcome.PingMs), TestLogColor.Cyan));
            else
                segments.Add(new TestLogSegment(PresetTestLog.Text("PT_StatusTimeout"), TestLogColor.Yellow));
        }
        else
        {
            segments.Add(new TestLogSegment(" " + PresetTestLog.Text("PT_ColPing") + ": ", TestLogColor.Gray));
            if (outcome.PingOk)
                segments.Add(new TestLogSegment(string.Format(PresetTestLog.Text("PT_Ms"), outcome.PingMs), TestLogColor.Cyan));
            else
                segments.Add(new TestLogSegment(PresetTestLog.Text("PT_StatusTimeout"), TestLogColor.Red));
        }

        return segments;
    }

    private static void Count(StandardOutcome outcome, PresetTestResult result)
    {
        if (outcome.PingOk)
            result.PingOk++;
        else
            result.PingFail++;
        if (!outcome.IsUrl)
            return;

        foreach (StandardToken token in outcome.Tokens)
        {
            switch (token.Kind)
            {
                case StdStatus.Ok:
                    result.HttpOk++;
                    break;
                case StdStatus.Unsupported:
                    result.Unsupported++;
                    break;
                default:
                    result.HttpErr++;
                    break;
            }
        }
    }

    private async Task<StandardOutcome> CheckTargetAsync(PresetTestTarget target, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var outcome = new StandardOutcome { Name = target.Name, IsUrl = !target.IsPing, Tokens = new List<StandardToken>() };

            if (!target.IsPing)
            {
                foreach (var test in CurlProcessRunner.ProtocolVariants)
                {
                    var argList = new List<string> { "-I", "-s", "-m", CurlProcessRunner.TimeoutSeconds.ToString(), "-o", "NUL", "-w", "%{http_code}", "--show-error" };
                    argList.AddRange(test.Args);
                    argList.Add(target.Url);

                    var (exit, _, stderr) = await _curl.RunAsync(argList, ct);
                    outcome.Tokens.Add(MakeToken(test.Label, Classify(exit, stderr)));
                }
            }

            var (pingOk, pingMs) = await CheckPingAsync(target.PingHost, ct);
            outcome.PingOk = pingOk;
            outcome.PingMs = pingMs;
            return outcome;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static StdStatus Classify(int exit, string stderr)
    {
        if (CurlErrorPatterns.Certificate.IsMatch(stderr ?? string.Empty))
            return StdStatus.Ssl;
        if (CurlErrorPatterns.Unsupported.IsMatch(stderr ?? string.Empty))
            return StdStatus.Unsupported;
        if (exit == 0)
            return StdStatus.Ok;
        return StdStatus.Error;
    }

    private static StandardToken MakeToken(string label, StdStatus kind)
    {
        return kind switch
        {
            StdStatus.Ok => new StandardToken { Label = label, Kind = kind, StatusText = PresetTestLog.Text("PT_StatusOk"), Color = TestLogColor.Green },
            StdStatus.Unsupported => new StandardToken { Label = label, Kind = kind, StatusText = PresetTestLog.Text("PT_StatusUnsupported"), Color = TestLogColor.Yellow },
            StdStatus.Ssl => new StandardToken { Label = label, Kind = kind, StatusText = PresetTestLog.Text("PT_StatusSsl"), Color = TestLogColor.Red },
            _ => new StandardToken { Label = label, Kind = kind, StatusText = PresetTestLog.Text("PT_StatusFail"), Color = TestLogColor.Red },
        };
    }

    private static async Task<(bool ok, long ms)> CheckPingAsync(string host, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(host))
            return (false, -1);
        try
        {
            using var ping = new Ping();
            long sum = 0;
            int ok = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                PingReply reply = await ping.SendPingAsync(host, 3000);
                if (reply.Status == IPStatus.Success)
                {
                    sum += reply.RoundtripTime;
                    ok++;
                }
            }
            if (ok > 0)
                return (true, sum / ok);
        }
        catch (OperationCanceledException) { throw; }
        // An unknown or unreachable host throws PingException: that is a failed ping, not an error.
        catch (PingException) { }
        return (false, -1);
    }

    private sealed class StandardToken
    {
        public string Label;
        public StdStatus Kind;
        public string StatusText;
        public TestLogColor Color;
    }

    private sealed class StandardOutcome
    {
        public string Name;
        public bool IsUrl;
        public List<StandardToken> Tokens;
        public bool PingOk;
        public long PingMs;
    }
}
