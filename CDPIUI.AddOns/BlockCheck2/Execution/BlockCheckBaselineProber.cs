using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Probes every target without a strategy, so the scan knows what is reachable already.
/// </summary>
internal static class BlockCheckBaselineProber
{
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

        int successfulChecks = 0;
        int failedChecks = 0;
        int completedChecks = 0;
        int totalChecks = targets.Count * options.AttemptsPerTarget;

        foreach (BlockCheckTarget target in targets)
        {
            List<ProbeAttempt> attempts = [];
            try
            {
                for (int attempt = 1; attempt <= options.AttemptsPerTarget; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new BlockCheckScanProgress(
                        BlockCheckScanPhase.BaselineProbing,
                        completedChecks,
                        totalChecks,
                        target.Id,
                        string.Empty,
                        attempt,
                        successfulChecks,
                        failedChecks));
                    try
                    {
                        ProbeAttempt probeAttempt = await probeRunner
                            .ProbeAsync(target, cancellationToken)
                            .ConfigureAwait(false);
                        attempts.Add(probeAttempt);
                        if (probeAttempt.Success)
                        {
                            successfulChecks++;
                        }
                        else
                        {
                            failedChecks++;
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        attempts.Add(BlockCheckScanIssues.FailedAttempt("baseline_probe_failed", exception.Message));
                        failedChecks++;
                        issues.Add(BlockCheckScanIssues.Error(
                            "SCAN_BASELINE_FAILED",
                            $"Baseline probe failed: {exception.Message}",
                            target.Id));
                    }

                    completedChecks++;
                    progress?.Report(new BlockCheckScanProgress(
                        BlockCheckScanPhase.BaselineProbing,
                        completedChecks,
                        totalChecks,
                        target.Id,
                        string.Empty,
                        attempt,
                        successfulChecks,
                        failedChecks));
                }
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

    private static ProbeResult CreateResult(BlockCheckTarget target, List<ProbeAttempt> attempts) => new()
    {
        StrategyId = string.Empty,
        TargetId = target.Id,
        Attempts = attempts,
    };
}
