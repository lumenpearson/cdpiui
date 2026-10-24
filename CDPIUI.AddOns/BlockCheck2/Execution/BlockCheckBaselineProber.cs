using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Probes every target without a strategy, so the scan knows what is reachable already.
/// </summary>
internal sealed class BlockCheckBaselineProber
{
    private readonly IBlockCheckProbeRunner _probeRunner;
    private readonly BlockCheckScanOptions _options;
    private readonly IProgress<BlockCheckScanProgress>? _progress;
    private readonly ICollection<BlockCheckIssue> _issues;
    private readonly CancellationToken _cancellationToken;
    private readonly int _totalChecks;

    private int _successfulChecks;
    private int _failedChecks;
    private int _completedChecks;

    private BlockCheckBaselineProber(
        IReadOnlyList<BlockCheckTarget> targets,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions options,
        IProgress<BlockCheckScanProgress>? progress,
        ICollection<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        _probeRunner = probeRunner;
        _options = options;
        _progress = progress;
        _issues = issues;
        _cancellationToken = cancellationToken;
        _totalChecks = targets.Count * options.AttemptsPerTarget;
    }

    public static async Task<BlockCheckBaselineOutcome> ProbeAsync(
        IReadOnlyList<BlockCheckTarget> targets,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions options,
        IProgress<BlockCheckScanProgress>? progress,
        ICollection<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        List<ProbeResult> results = [];
        if (!options.RunBaseline)
        {
            return new BlockCheckBaselineOutcome(results, false);
        }

        var prober = new BlockCheckBaselineProber(targets, probeRunner, options, progress, issues, cancellationToken);
        foreach (BlockCheckTarget target in targets)
        {
            List<ProbeAttempt> attempts = [];
            try
            {
                await prober.ProbeTargetAsync(target, attempts).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (attempts.Count > 0)
                {
                    results.Add(CreateResult(target, attempts));
                }

                return new BlockCheckBaselineOutcome(results, true);
            }

            results.Add(CreateResult(target, attempts));
        }

        return new BlockCheckBaselineOutcome(results, false);
    }

    private async Task ProbeTargetAsync(BlockCheckTarget target, List<ProbeAttempt> attempts)
    {
        for (int attempt = 1; attempt <= _options.AttemptsPerTarget; attempt++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            Report(target, attempt);
            try
            {
                ProbeAttempt probeAttempt = await _probeRunner
                    .ProbeAsync(target, _cancellationToken)
                    .ConfigureAwait(false);
                attempts.Add(probeAttempt);
                if (probeAttempt.Success)
                {
                    _successfulChecks++;
                }
                else
                {
                    _failedChecks++;
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                attempts.Add(BlockCheckScanIssues.FailedAttempt("baseline_probe_failed", exception.Message));
                _failedChecks++;
                _issues.Add(BlockCheckScanIssues.Error(
                    "SCAN_BASELINE_FAILED",
                    $"Baseline probe failed: {exception.Message}",
                    target.Id));
            }

            _completedChecks++;
            Report(target, attempt);
        }
    }

    private void Report(BlockCheckTarget target, int attempt) =>
        _progress?.Report(new BlockCheckScanProgress(
            BlockCheckScanPhase.BaselineProbing,
            _completedChecks,
            _totalChecks,
            target.Id,
            string.Empty,
            attempt,
            _successfulChecks,
            _failedChecks));

    private static ProbeResult CreateResult(BlockCheckTarget target, List<ProbeAttempt> attempts) => new()
    {
        StrategyId = string.Empty,
        TargetId = target.Id,
        Attempts = attempts,
    };
}
