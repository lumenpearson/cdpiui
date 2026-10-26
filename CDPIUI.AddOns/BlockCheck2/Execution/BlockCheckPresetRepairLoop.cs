using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Validates the combined preset and, while it fails, excludes the strategies of the failing
/// routes and synthesizes the preset again from the remaining scan results.
/// </summary>
internal sealed class BlockCheckPresetRepairLoop
{
    private readonly BlockCheckSynthesisService _synthesisService;
    private readonly StrategyCatalog _catalog;
    private readonly BlockCheckTarget[] _targets;
    private readonly ProbeResult[] _originalProbeResults;
    private readonly ProbeResult[] _baselineResults;
    private readonly BlockCheckSynthesisOptions _synthesisOptions;
    private readonly IBlockCheckStrategyRunner _strategyRunner;
    private readonly IBlockCheckProbeRunner _probeRunner;
    private readonly BlockCheckPresetValidationOptions _options;
    private readonly IProgress<BlockCheckScanProgress>? _progress;
    private readonly List<BlockCheckIssue> _issues;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<string, BlockCheckTarget> _targetsById;
    private readonly Dictionary<string, RuntimeRouteKey> _routesByTargetId;
    private readonly Dictionary<string, string> _signaturesByStrategyId;
    private readonly HashSet<BlockCheckStrategyRouteExclusion> _exclusions = [];

    public BlockCheckPresetRepairLoop(
        BlockCheckSynthesisService synthesisService,
        StrategyCatalog catalog,
        BlockCheckTarget[] targets,
        ProbeResult[] originalProbeResults,
        ProbeResult[] baselineResults,
        BlockCheckSynthesisOptions synthesisOptions,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckPresetValidationOptions options,
        IProgress<BlockCheckScanProgress>? progress,
        List<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        _synthesisService = synthesisService;
        _catalog = catalog;
        _targets = targets;
        _originalProbeResults = originalProbeResults;
        _baselineResults = baselineResults;
        _synthesisOptions = synthesisOptions;
        _strategyRunner = strategyRunner;
        _probeRunner = probeRunner;
        _options = options;
        _progress = progress;
        _issues = issues;
        _cancellationToken = cancellationToken;
        _targetsById = targets
            .ToDictionary(target => target.Id, StringComparer.Ordinal);
        _routesByTargetId = targets
            .ToDictionary(target => target.Id, target => target.GetRuntimeRouteKey(), StringComparer.Ordinal);
        _signaturesByStrategyId = catalog.Strategies
            .ToDictionary(
                strategy => strategy.Id,
                CandidateNormalizer.GetPlanSignature,
                StringComparer.OrdinalIgnoreCase);
    }

    public BlockCheckSynthesisResult CurrentSynthesis { get; private set; } = null!;

    /// <summary>
    /// Runs until the preset passes or cannot be repaired; <see cref="CurrentSynthesis"/> then holds
    /// the last synthesized preset.
    /// </summary>
    public async Task RunAsync(
        BlockCheckSynthesisResult initialSynthesis,
        List<BlockCheckPresetValidationAttempt> attempts)
    {
        CurrentSynthesis = initialSynthesis;
        int repairsApplied = 0;
        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            BlockCheckPresetValidationAttempt attempt = await new BlockCheckPresetCandidateRun(
                    _targets,
                    _strategyRunner,
                    _probeRunner,
                    _options,
                    attempts.Count + 1,
                    _progress,
                    _issues,
                    _cancellationToken)
                .RunAsync(
                    CurrentSynthesis.Configuration.CommandLine,
                    BlockCheckPresetRepairPlanner.GetExcludedStrategyIds(_exclusions, _signaturesByStrategyId))
                .ConfigureAwait(false);
            attempts.Add(attempt);

            if (attempt.InfrastructureFailure)
            {
                break;
            }

            if (attempt.Success)
            {
                if (repairsApplied > 0)
                {
                    _issues.Add(new BlockCheckIssue(
                        BlockCheckIssueSeverity.Info,
                        "PRESET_REPAIRED",
                        $"The combined preset passed after {repairsApplied} repair iteration(s)."));
                }
                break;
            }

            if (!TryRepair(attempt, repairsApplied))
            {
                break;
            }

            repairsApplied++;
            if (!CurrentSynthesis.Success)
            {
                _issues.Add(BlockCheckScanIssues.Error(
                    "PRESET_REPAIR_NO_CANDIDATE",
                    "No remaining compatible strategy can replace the failing preset profile."));
                break;
            }
        }
    }

    private bool TryRepair(BlockCheckPresetValidationAttempt attempt, int repairsApplied)
    {
        if (!_options.EnableRepair)
        {
            _issues.Add(BlockCheckScanIssues.Error(
                "PRESET_VALIDATION_FAILED",
                "The synthesized preset failed combined validation and automatic repair is disabled."));
            return false;
        }

        if (repairsApplied >= _options.MaximumRepairIterations)
        {
            _issues.Add(BlockCheckScanIssues.Error(
                "PRESET_REPAIR_LIMIT_REACHED",
                "The synthesized preset still fails after the configured repair iterations."));
            return false;
        }

        HashSet<BlockCheckStrategyRouteExclusion> newExclusions = BlockCheckPresetRepairPlanner.FindExclusions(
            attempt.FailedTargetIds,
            CurrentSynthesis.Profiles,
            _targetsById,
            _routesByTargetId);
        newExclusions.ExceptWith(_exclusions);
        if (newExclusions.Count == 0)
        {
            _issues.Add(BlockCheckScanIssues.Error(
                "PRESET_REPAIR_STALLED",
                "The failing targets could not be mapped to a replaceable preset strategy."));
            return false;
        }

        _exclusions.UnionWith(newExclusions);
        _progress?.Report(new BlockCheckScanProgress(
            BlockCheckScanPhase.RepairingPreset,
            repairsApplied,
            _options.MaximumRepairIterations,
            attempt.FailedTargetIds.OrderBy(id => id, StringComparer.Ordinal).First(),
            string.Join(',', BlockCheckPresetRepairPlanner.GetExcludedStrategyIds(newExclusions, _signaturesByStrategyId)
                .OrderBy(id => id, StringComparer.Ordinal))));

        ProbeResult[] remainingResults = _originalProbeResults
            .Where(result => !BlockCheckPresetRepairPlanner.IsExcluded(
                result,
                _exclusions,
                _routesByTargetId,
                _signaturesByStrategyId))
            .ToArray();
        CurrentSynthesis = _synthesisService.Synthesize(
            _catalog,
            _targets,
            remainingResults,
            _synthesisOptions,
            _baselineResults);
        return true;
    }
}
