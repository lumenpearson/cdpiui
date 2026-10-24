using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed class BlockCheckPresetValidationService
{
    private readonly BlockCheckSynthesisService _synthesisService;

    public BlockCheckPresetValidationService(BlockCheckSynthesisService? synthesisService = null)
    {
        _synthesisService = synthesisService ?? new BlockCheckSynthesisService();
    }

    public async Task<BlockCheckPresetValidationResult> ValidateAndRepairAsync(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IEnumerable<ProbeResult> scanProbeResults,
        IEnumerable<ProbeResult> baselineProbeResults,
        BlockCheckSynthesisResult initialSynthesis,
        BlockCheckSynthesisOptions synthesisOptions,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckPresetValidationOptions? options = null,
        IProgress<BlockCheckScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(scanProbeResults);
        ArgumentNullException.ThrowIfNull(baselineProbeResults);
        ArgumentNullException.ThrowIfNull(initialSynthesis);
        ArgumentNullException.ThrowIfNull(synthesisOptions);
        ArgumentNullException.ThrowIfNull(strategyRunner);
        ArgumentNullException.ThrowIfNull(probeRunner);

        options ??= new BlockCheckPresetValidationOptions();
        BlockCheckTarget[] targetArray = targets.ToArray();
        ProbeResult[] originalProbeResults = scanProbeResults.ToArray();
        ProbeResult[] baselineResults = baselineProbeResults.ToArray();
        List<BlockCheckIssue> issues = [];
        if (!initialSynthesis.Success)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "PRESET_SYNTHESIS_INVALID",
                "Preset validation requires a successful initial synthesis result."));
        }

        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return Result(false, [], initialSynthesis, issues);
        }

        if (!options.Enabled)
        {
            return Result(false, [], initialSynthesis, issues);
        }

        issues.AddRange(ValidateOptions(options));
        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return Result(false, [], initialSynthesis, issues);
        }

        if (initialSynthesis.Profiles.Count == 0)
        {
            return Result(false, [], initialSynthesis, issues);
        }

        var repairLoop = new BlockCheckPresetRepairLoop(
            _synthesisService,
            catalog,
            targetArray,
            originalProbeResults,
            baselineResults,
            synthesisOptions,
            strategyRunner,
            probeRunner,
            options,
            progress,
            issues,
            cancellationToken);
        List<BlockCheckPresetValidationAttempt> attempts = [];

        if (!await TryPrepareAsync(strategyRunner, issues, cancellationToken).ConfigureAwait(false))
        {
            return Result(true, attempts, initialSynthesis, issues);
        }

        try
        {
            await repairLoop.RunAsync(initialSynthesis, attempts).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                issues.Add(BlockCheckScanIssues.Error(
                    "PRESET_VALIDATION_COMPLETE_FAILED",
                    $"Could not restore the process environment after preset validation: {exception.Message}"));
            }
        }

        return Result(true, attempts, repairLoop.CurrentSynthesis, issues);
    }

    /// <summary>
    /// Prepares the process environment. A failed preparation is restored at once; the caller
    /// restores it after a successful one.
    /// </summary>
    private static async Task<bool> TryPrepareAsync(
        IBlockCheckStrategyRunner strategyRunner,
        List<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            await strategyRunner.PrepareAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Cancellation still has to propagate; cleanup was attempted with a non-cancelable token.
            }
            throw;
        }
        catch (Exception exception)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "PRESET_VALIDATION_PREPARE_FAILED",
                $"Could not prepare the preset validation environment: {exception.Message}"));
            try
            {
                await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception completeException)
            {
                issues.Add(BlockCheckScanIssues.Error(
                    "PRESET_VALIDATION_COMPLETE_FAILED",
                    $"Could not restore the process environment after failed preparation: {completeException.Message}"));
            }
            return false;
        }
    }

    private static IReadOnlyList<BlockCheckIssue> ValidateOptions(
        BlockCheckPresetValidationOptions options)
    {
        List<BlockCheckIssue> issues = [];
        if (options.AttemptsPerTarget < 1)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "PRESET_VALIDATION_ATTEMPTS_INVALID",
                "Preset validation attempts per target must be at least one."));
        }

        if (!double.IsFinite(options.MinimumSuccessRate) ||
            options.MinimumSuccessRate <= 0d ||
            options.MinimumSuccessRate > 1d)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "PRESET_VALIDATION_RATE_INVALID",
                "Preset validation success rate must be greater than zero and at most one."));
        }

        if (options.MaximumRepairIterations < 0)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "PRESET_REPAIR_ITERATIONS_INVALID",
                "Maximum preset repair iterations cannot be negative."));
        }

        return issues;
    }

    private static BlockCheckPresetValidationResult Result(
        bool validationRequired,
        IReadOnlyList<BlockCheckPresetValidationAttempt> attempts,
        BlockCheckSynthesisResult synthesis,
        IReadOnlyList<BlockCheckIssue> issues) => new()
        {
            ValidationRequired = validationRequired,
            Attempts = attempts,
            FinalSynthesis = synthesis,
            Issues = issues,
        };
}
