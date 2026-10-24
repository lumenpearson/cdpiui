using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2ResultProbeItem
{
    public BlockCheck2ResultProbeItem(
        BlockCheckReportProbe probe,
        BlockCheckReportTarget? target = null)
    {
        Kind = probe.Kind;
        StrategyId = string.IsNullOrWhiteSpace(probe.StrategyId) ? "baseline" : probe.StrategyId;
        TargetId = probe.TargetId;
        TargetUrl = target == null
            ? probe.TargetId
            : BlockCheckTargetDisplayFormatter.FormatUrl(
                target.Host,
                target.Path,
                target.Protocol,
                target.Port);
        ConnectionDetails = target == null
            ? string.Empty
            : BlockCheckTargetDisplayFormatter.FormatConnectionDetails(
                target.Protocol,
                target.IpVersion,
                target.Transport,
                target.Port);
        Attempts = probe.AttemptCount;
        Successes = probe.SuccessCount;
        SuccessRate = $"{probe.SuccessRate:P0}";
        Median = probe.MedianTimeToFirstByteMs.HasValue
            ? $"{probe.MedianTimeToFirstByteMs.Value:0} ms"
            : "—";
        string status = probe.HttpStatusCodes.Count > 0
            ? $" · HTTP {string.Join('/', probe.HttpStatusCodes)}"
            : string.Empty;
        string failures = probe.FailureCodes.Count > 0
            ? $" · {string.Join(", ", probe.FailureCodes)}"
            : string.Empty;
        Summary = $"{Successes}/{Attempts} · {SuccessRate} · {Median}{status}{failures}";
    }

    public string Kind { get; }
    public string StrategyId { get; }
    public string TargetId { get; }
    public string TargetUrl { get; }
    public string ConnectionDetails { get; }
    public int Attempts { get; }
    public int Successes { get; }
    public string SuccessRate { get; }
    public string Median { get; }
    public string Summary { get; }
}
