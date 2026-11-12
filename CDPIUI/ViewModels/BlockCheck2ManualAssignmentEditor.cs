#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.ViewModels;

// Edits the existing draft assignments; preset rebuilding and UI notifications remain with the host.
internal sealed class BlockCheck2ManualAssignmentEditor
{
    private readonly BlockCheck2PresetDraft _draft;
    private readonly Func<BlockCheckResultSession?> _session;
    private readonly Func<IReadOnlyList<BlockCheck2StrategyEvidenceItem>> _evidence;
    private readonly Action _clearAcknowledgedIssues;
    private readonly Action<bool> _rebuildPreset;
    private readonly Func<StrategyDefinition, string> _formatActions;
    private readonly HashSet<string> _automaticAssignmentKeys = new(StringComparer.OrdinalIgnoreCase);

    public BlockCheck2ManualAssignmentEditor(BlockCheck2PresetDraft draft,
        Func<BlockCheckResultSession?> session, Func<IReadOnlyList<BlockCheck2StrategyEvidenceItem>> evidence,
        Action clearAcknowledgedIssues, Action<bool> rebuildPreset, Func<StrategyDefinition, string>? formatActions = null)
    {
        _draft = draft;
        _session = session;
        _evidence = evidence;
        _clearAcknowledgedIssues = clearAcknowledgedIssues;
        _rebuildPreset = rebuildPreset;
        _formatActions = formatActions ?? (strategy => new Zapret2ConfigWriter().FormatStrategyActions(strategy));
    }

    public ObservableCollection<BlockCheck2ManualAssignmentItem> Items => _draft.Assignments;

    public void ClearAutomaticKeys() => _automaticAssignmentKeys.Clear();

    public bool AddForSite(BlockCheck2StrategyEvidenceItem item) =>
        AddAssignment(new BlockCheckManualAssignment(
            item.StrategyId,
            item.TargetId,
            BlockCheckManualScopeKind.Site));

    public bool AddForSiteList(BlockCheck2StrategyEvidenceItem item, string siteListPath)
    {
        BlockCheckTarget? anchor = FindCompatibleSiteListTargets(item)
            .FirstOrDefault(target => target.HostListPaths.Contains(
                siteListPath,
                StringComparer.OrdinalIgnoreCase));
        return anchor != null && AddAssignment(new BlockCheckManualAssignment(
            item.StrategyId,
            anchor.Id,
            BlockCheckManualScopeKind.SiteList,
            siteListPath));
    }

    public void RemoveAssignment(BlockCheck2ManualAssignmentItem item)
    {
        int index = Items.IndexOf(item);
        if (index < 0)
        {
            return;
        }
        _clearAcknowledgedIssues();
        Items.RemoveAt(index);
        _rebuildPreset(true);
    }

    public bool MoveAssignment(BlockCheck2ManualAssignmentItem item, int offset)
    {
        int oldIndex = Items.IndexOf(item);
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Items.Count)
        {
            return false;
        }
        _clearAcknowledgedIssues();
        Items.Move(oldIndex, newIndex);
        _rebuildPreset(true);
        return true;
    }

    public void ClearManualAssignments()
    {
        _clearAcknowledgedIssues();
        Items.Clear();
        _rebuildPreset(true);
    }

    public IReadOnlyList<string> GetSiteListPaths(BlockCheck2StrategyEvidenceItem item) =>
        FindCompatibleSiteListTargets(item)
            .SelectMany(target => target.HostListPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public bool CanAddForSiteList(BlockCheck2StrategyEvidenceItem item) =>
        FindCompatibleSiteListTargets(item).Any();

    private bool AddAssignment(BlockCheckManualAssignment assignment)
    {
        if (_session() == null)
        {
            return false;
        }
        bool duplicate = Items.Any(item =>
            string.Equals(item.Assignment.StrategyId, assignment.StrategyId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Assignment.AnchorTargetId, assignment.AnchorTargetId, StringComparison.Ordinal) &&
            item.Assignment.ScopeKind == assignment.ScopeKind &&
            string.Equals(item.Assignment.SiteListPath, assignment.SiteListPath, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return false;
        }

        StrategyDefinition? strategy = _session()!.Catalog.Strategies.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assignment.StrategyId, StringComparison.OrdinalIgnoreCase));
        BlockCheckTarget? target = _session()!.Targets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assignment.AnchorTargetId, StringComparison.Ordinal));
        if (strategy == null || target == null)
        {
            return false;
        }
        _clearAcknowledgedIssues();
        AddAssignmentCore(assignment, strategy, target);
        _rebuildPreset(true);
        return true;
    }

    private void AddAssignmentCore(
        BlockCheckManualAssignment assignment,
        StrategyDefinition strategy,
        BlockCheckTarget target)
    {
        Items.Add(new BlockCheck2ManualAssignmentItem(
            assignment,
            strategy,
            target,
            _formatActions(strategy)));
    }

    private IEnumerable<BlockCheckTarget> FindCompatibleSiteListTargets(
        BlockCheck2StrategyEvidenceItem item)
    {
        if (_session() == null)
        {
            return [];
        }

        BlockCheckTarget source = item.Evidence.Target;
        string host = BlockCheckTarget.NormalizeHost(source.Host);
        return _session()!.Targets.Where(target =>
            !_session()!.RunResult.Scan.IgnoredTargetIds.Contains(target.Id) &&
            BlockCheck2ManualAssignmentRules.IsCompatibleSiteListTarget(target, source, host));
    }

    public void SeedAutomaticAssignments(BlockCheckResultSession value)
    {
        _clearAcknowledgedIssues();
        Items.Clear();
        Dictionary<string, StrategyDefinition> strategies = value.Catalog.Strategies
            .ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BlockCheckTarget> targets = value.Targets
            .ToDictionary(target => target.Id, StringComparer.Ordinal);

        foreach (BlockCheckReportProfile profile in value.Report.Profiles)
        {
            BlockCheckTarget[] profileTargets = profile.TargetIds
                .Select(id => targets.GetValueOrDefault(id))
                .Where(target => target != null)
                .Cast<BlockCheckTarget>()
                .ToArray();
            IEnumerable<(BlockCheckTarget Target, BlockCheckManualScopeKind Scope, string? Path)> scopes =
                BlockCheck2ManualAssignmentRules.GetAutomaticScopes(profile, profileTargets);

            foreach ((BlockCheckTarget target, BlockCheckManualScopeKind scope, string? path) in scopes)
            {
                foreach (string strategyId in new[] { profile.PrimaryStrategyId }.Concat(profile.FallbackStrategyIds))
                {
                    if (!strategies.TryGetValue(strategyId, out StrategyDefinition? strategy))
                    {
                        continue;
                    }
                    BlockCheckManualAssignment assignment = new(strategyId, target.Id, scope, path);
                    bool duplicate = Items.Any(item => item.Assignment == assignment);
                    if (!duplicate)
                    {
                        AddAssignmentCore(assignment, strategy, target);
                        _automaticAssignmentKeys.Add(BlockCheck2ManualAssignmentRules.AssignmentKey(assignment));
                    }
                }
            }
        }

        _rebuildPreset(false);
        _draft.LoadAutomatic(value.Report.PresetArguments, value.Report.Profiles);
        RefreshAssignmentIndexes();
        RefreshEvidenceSelections();
    }

    public void RefreshEvidenceSelections()
    {
        foreach (BlockCheck2StrategyEvidenceItem evidence in _evidence())
        {
            BlockCheck2ManualAssignmentItem[] selectedAssignments = Items
                .Where(item => BlockCheck2ManualAssignmentRules.AssignmentMatchesEvidence(item, evidence))
                .ToArray();
            bool selected = selectedAssignments.Length > 0;
            bool automatic = selectedAssignments.Any(item =>
                _automaticAssignmentKeys.Contains(BlockCheck2ManualAssignmentRules.AssignmentKey(item.Assignment)));
            evidence.SetSelection(selected, automatic);
        }
    }

    public void RefreshAssignmentIndexes()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            Items[index].SetOrder(index + 1);
        }
    }

}
