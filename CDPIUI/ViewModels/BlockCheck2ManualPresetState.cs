#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

// Rebuilds the same draft while keeping UI issue construction and notifications with the host.
internal sealed class BlockCheck2ManualPresetState
{
    private readonly BlockCheck2PresetDraft _draft;
    private readonly IReadOnlyList<ProbeResult> _manualTests;
    private readonly IBlockCheck2ManualPresetHost _host;
    private readonly IBlockCheck2ManualPresetOperations _operations;
    private BlockCheckManualPresetResult? _manualPreset;

    public BlockCheck2ManualPresetState(BlockCheck2PresetDraft draft, IReadOnlyList<ProbeResult> manualTests,
        IBlockCheck2ManualPresetHost host, IBlockCheck2ManualPresetOperations operations)
    {
        _draft = draft;
        _manualTests = manualTests;
        _host = host;
        _operations = operations;
    }

    public string Arguments => _manualPreset?.Configuration.CommandLine ?? string.Empty;
    public void ClearResult() => _manualPreset = null;

    public void Rebuild(bool updateDraft)
    {
        _host.ClearIssues();
        if (_host.Session! == null || _draft.Assignments.Count == 0)
        {
            _manualPreset = null;
            _host.RefreshAssignmentIndexes();
            if (updateDraft)
            {
                _draft.ApplyStructuredChange(string.Empty, []);
            }
            _host.RefreshEvidenceSelections();
            _host.NotifyManualState();
            return;
        }

        IEnumerable<ProbeResult> evidence = _host.Session!.RunResult.Scan.ProbeResults
            .Where(result => !_manualTests.Any(test =>
                string.Equals(test.StrategyId, result.StrategyId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(test.TargetId, result.TargetId, StringComparison.Ordinal)))
            .Concat(_manualTests);
        _manualPreset = _operations.Build(
            _host.Session!.Catalog,
            _host.Session!.Targets.Where(target => !_host.Session!.RunResult.Scan.IgnoredTargetIds.Contains(target.Id)),
            _draft.Assignments.Select(item => item.Assignment),
            evidence,
            OptionsFor(_host.Session!));
        foreach (var group in _manualPreset.Issues.GroupBy(issue => new
        {
            issue.Severity,
            issue.Code,
            issue.Message,
        }))
        {
            _host.AddIssueGroup(
                group.Key.Severity,
                group.Key.Code,
                group.Key.Message,
                group.Count(),
                group.Select(issue => DescribeManualIssueSubject(issue.SubjectId)));
        }
        if (updateDraft)
        {
            _draft.ApplyStructuredChange(
                _manualPreset.Configuration.CommandLine,
                _manualPreset.Profiles);
        }
        _host.RefreshAssignmentIndexes();
        _host.RefreshEvidenceSelections();
        _host.NotifyManualState();
    }

    private string DescribeManualIssueSubject(string? subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return string.Empty;
        }

        BlockCheck2ManualAssignmentItem? scopeAssignment = _draft.Assignments.FirstOrDefault(item =>
            string.Equals(BlockCheck2ManualAssignmentRules.ManualScopeKey(item), subjectId, StringComparison.OrdinalIgnoreCase));
        if (scopeAssignment != null)
        {
            return $"{scopeAssignment.Scope} · {scopeAssignment.ConnectionDetails}";
        }

        BlockCheck2ManualAssignmentItem? assignment = _draft.Assignments.FirstOrDefault(item =>
            string.Equals(item.Assignment.StrategyId, subjectId, StringComparison.OrdinalIgnoreCase));
        if (assignment != null)
        {
            return BlockCheck2ManualAssignmentRules.DescribeStrategySubject(assignment, subjectId);
        }

        string[] strategyIds = subjectId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (strategyIds.Length > 1)
        {
            string[] strategyNames = strategyIds
                .Select(strategyId =>
                {
                    BlockCheck2ManualAssignmentItem? item = _draft.Assignments.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.Assignment.StrategyId,
                            strategyId,
                            StringComparison.OrdinalIgnoreCase));
                    return item == null
                        ? strategyId
                        : BlockCheck2ManualAssignmentRules.DescribeStrategySubject(item, strategyId);
                })
                .ToArray();
            return string.Join(" / ", strategyNames);
        }

        return subjectId;
    }

    public static BlockCheckManualPresetOptions OptionsFor(BlockCheckResultSession session) => new()
    {
        MinimumAttempts = Math.Max(1, session.RunOptions.Synthesis.MinimumAttempts),
        MinimumSuccessRate = session.RunOptions.Synthesis.MinimumSuccessRate,
    };

}
