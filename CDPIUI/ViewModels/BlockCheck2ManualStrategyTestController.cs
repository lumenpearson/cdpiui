#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Shared.Models;

namespace CDPIUI.ViewModels;

internal sealed class BlockCheck2ManualStrategyTestController
{
    private readonly BlockCheck2PresetDraft _draft;
    private readonly List<ProbeResult> _manualTests;
    private readonly Func<BlockCheckResultSession?> _session;
    private readonly Action<bool> _rebuildPreset;
    private readonly IBlockCheck2ManualStrategyTestOperations _operations;

    public BlockCheck2ManualStrategyTestController(BlockCheck2PresetDraft draft, List<ProbeResult> manualTests,
        Func<BlockCheckResultSession?> session, Action<bool> rebuildPreset, IBlockCheck2ManualStrategyTestOperations operations)
    {
        _draft = draft;
        _manualTests = manualTests;
        _session = session;
        _rebuildPreset = rebuildPreset;
        _operations = operations;
    }

    public OperationResultModel<string> GetFullStrategyArguments(BlockCheck2StrategyEvidenceItem item) =>
        _operations.GetFullStrategyArguments(
            item.Evidence.Strategy,
            item.Evidence.Target,
            new BlockCheckManualStrategyTestOptions
            {
                Attempts = Math.Max(1, _session()!.RunOptions.Scan.AttemptsPerTarget),
            });

    public async Task<BlockCheckManualStrategyTestResult> TestStrategyAsync(
        BlockCheck2StrategyEvidenceItem item, CancellationToken cancellationToken = default)
    {
        if (_session() == null)
        {
            return new BlockCheckManualStrategyTestResult
            {
                Issues =
                [
                    new BlockCheckIssue(
                        BlockCheckIssueSeverity.Error,
                        "MANUAL_SESSION_UNAVAILABLE",
                        "The original BlockCheck session is unavailable; this report cannot launch a live strategy test."),
                ],
            };
        }

        BlockCheckManualStrategyTestResult result = await _operations.TestAsync(
                item.Evidence.Strategy,
                item.Evidence.Target,
                new BlockCheckManualStrategyTestOptions
                {
                    Attempts = Math.Max(1, _session()!.RunOptions.Scan.AttemptsPerTarget),
                },
                new CdpiuiZapret2StrategyRunnerOptions
                {
                    StartupGracePeriod = _session()!.StartupGracePeriod,
                },
                new CurlBlockCheckProbeRunnerOptions
                {
                    ConnectTimeout = _session()!.ConnectTimeout,
                    RequestTimeout = _session()!.RequestTimeout,
                },
                cancellationToken)
            .ConfigureAwait(true);
        if (result.ProbeResult != null)
        {
            _manualTests.RemoveAll(probe =>
                string.Equals(probe.StrategyId, result.ProbeResult.StrategyId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(probe.TargetId, result.ProbeResult.TargetId, StringComparison.Ordinal));
            _manualTests.Add(result.ProbeResult);
            item.ApplyTestResult(result.ProbeResult);
            _rebuildPreset(!_draft.IsAutomatic);
        }
        return result;
    }
}
