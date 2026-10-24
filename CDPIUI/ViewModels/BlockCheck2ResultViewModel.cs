using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.AddOns.BlockCheck2.Synthesis;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Shared.Models;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2ResultViewModel : ObservableObject
{
    private readonly BlockCheckManualPresetService _manualPresetService = new();
    private readonly BlockCheckManualStrategyTestService _manualTestService = new();
    private BlockCheckReport? _report;
    private BlockCheckResultSession? _session;
    private IReadOnlyList<BlockCheck2StrategyEvidenceItem> _allStrategyEvidence = [];
    private readonly List<ProbeResult> _manualTestResults = [];
    private readonly HashSet<string> _automaticAssignmentKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _acknowledgedManualIssueKeys = new(StringComparer.Ordinal);
    private BlockCheckManualPresetResult? _manualPreset;

    public ObservableCollection<BlockCheck2ResultProfileItem> Profiles { get; } = [];
    public ObservableCollection<BlockCheck2ResultProbeItem> Probes { get; } = [];
    public ObservableCollection<BlockCheckIssue> Issues { get; } = [];
    public ObservableCollection<BlockCheck2StrategyEvidenceItem> StrategyEvidence { get; } = [];
    public ObservableCollection<BlockCheck2SiteStrategyGroup> StrategyGroups { get; } = [];
    public BlockCheck2PresetDraft Draft { get; } = new();
    public ObservableCollection<BlockCheck2ManualAssignmentItem> ManualAssignments => Draft.Assignments;
    public ObservableCollection<BlockCheck2ManualIssueItem> ManualIssues { get; } = [];

    public BlockCheckReport? Report => _report;
    public bool HasReport => _report != null;
    public bool HasSession => _session != null;
    public bool Success => _report?.Success == true;
    public bool IsBestEffort => _report?.IsBestEffort == true;
    public bool NoBypassRequired => _report?.Issues.Any(issue =>
        string.Equals(issue.Code, "NO_BYPASS_REQUIRED", StringComparison.Ordinal)) == true;
    public bool IsHttpOnly => _report?.Targets.Count > 0 &&
        _report.Targets.All(target => target.Protocol == BlockCheckProtocol.Http);
    public bool HasStrategyEvidence => StrategyEvidence.Count > 0;
    public bool HasManualAssignments => ManualAssignments.Count > 0;
    public bool CanUseConfig => Draft.CanUseConfig;
    public string PresetArguments => _report?.PresetArguments ?? string.Empty;
    public string ManualPresetArguments => _manualPreset?.Configuration.CommandLine ?? string.Empty;
    public string EffectivePresetArguments => Draft.EffectiveArguments;
    public int TargetCount => _report?.Targets.Count ?? 0;
    public int ProfileCount => _report?.Profiles.Count ?? 0;
    public int ProbeCount => _report?.Probes.Count ?? 0;
    public int IssueCount => _report?.Issues.Count ?? 0;
    public string CreatedAtLocalText => _report == null
        ? string.Empty
        : _report.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string RunPresetText => _report?.RunPreset.ToString() ?? string.Empty;
    public int StrategyEvidenceCount => _allStrategyEvidence.Count;
    public int VisibleStrategyEvidenceCount => StrategyEvidence.Count;
    public int ManualAssignmentCount => ManualAssignments.Count;

    public BlockCheck2ResultViewModel()
    {
        Draft.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(BlockCheck2PresetDraft.EffectiveArguments) or
                nameof(BlockCheck2PresetDraft.CanUseConfig))
            {
                Notify(nameof(EffectivePresetArguments));
                Notify(nameof(CanUseConfig));
            }
        };
    }

    public void Load(BlockCheckReport value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _report = value;
        _session = null;
        _allStrategyEvidence = [];
        _manualTestResults.Clear();
        _automaticAssignmentKeys.Clear();
        _acknowledgedManualIssueKeys.Clear();
        StrategyEvidence.Clear();
        StrategyGroups.Clear();
        ManualAssignments.Clear();
        ManualIssues.Clear();
        _manualPreset = null;
        Draft.LoadAutomatic(value.PresetArguments, value.Profiles);
        Profiles.Clear();
        foreach (BlockCheckReportProfile profile in value.Profiles)
        {
            Profiles.Add(new BlockCheck2ResultProfileItem(profile));
        }

        Probes.Clear();
        var targetsById = value.Targets.ToDictionary(target => target.Id, StringComparer.Ordinal);
        foreach (BlockCheckReportProbe probe in value.Probes)
        {
            targetsById.TryGetValue(probe.TargetId, out BlockCheckReportTarget? target);
            Probes.Add(new BlockCheck2ResultProbeItem(probe, target));
        }

        Issues.Clear();
        foreach (BlockCheckIssue issue in value.Issues)
        {
            Issues.Add(BlockCheck2IssueText.Localize(issue));
        }

        Notify(nameof(Report));
        Notify(nameof(HasReport));
        Notify(nameof(HasSession));
        Notify(nameof(Success));
        Notify(nameof(IsBestEffort));
        Notify(nameof(NoBypassRequired));
        Notify(nameof(IsHttpOnly));
        Notify(nameof(CanUseConfig));
        Notify(nameof(PresetArguments));
        Notify(nameof(ManualPresetArguments));
        Notify(nameof(EffectivePresetArguments));
        Notify(nameof(TargetCount));
        Notify(nameof(ProfileCount));
        Notify(nameof(ProbeCount));
        Notify(nameof(IssueCount));
        Notify(nameof(CreatedAtLocalText));
        Notify(nameof(RunPresetText));
        NotifyManualState();
    }

    public void Load(BlockCheckResultSession value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Load(value.Report);
        _session = value;
        BlockCheckManualPresetOptions options = ManualOptions(value);

        _allStrategyEvidence = _manualPresetService.BuildEvidence(
                value.Catalog,
                value.Targets,
                value.RunResult.Scan.ProbeResults,
                value.RunResult.Scan.BaselineResults,
                value.RunResult.Scan.IgnoredTargetIds,
                options)
            .Select(evidence => new BlockCheck2StrategyEvidenceItem(evidence))
            .ToArray();
        SeedAutomaticAssignments(value);
        ApplyStrategyView(string.Empty, 0, 0);
        Notify(nameof(HasSession));
        NotifyManualState();
    }

    public void ApplyStrategyView(string? searchText, int sortMode, int filterMode)
    {
        IEnumerable<BlockCheck2StrategyEvidenceItem> query = BlockCheck2StrategyEvidenceView.Apply(
            _allStrategyEvidence,
            searchText,
            sortMode,
            filterMode);

        StrategyEvidence.Clear();
        StrategyGroups.Clear();
        foreach (BlockCheck2StrategyEvidenceItem item in query)
        {
            StrategyEvidence.Add(item);
            BlockCheck2SiteStrategyGroup? group = StrategyGroups.FirstOrDefault(candidate =>
                string.Equals(candidate.SiteUrl, item.SiteUrl, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                group = new BlockCheck2SiteStrategyGroup(item.SiteUrl);
                StrategyGroups.Add(group);
            }
            group.Add(item);
        }
        RefreshEvidenceSelections();
        Notify(nameof(HasStrategyEvidence));
        Notify(nameof(VisibleStrategyEvidenceCount));
    }

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
        int index = ManualAssignments.IndexOf(item);
        if (index < 0)
        {
            return;
        }
        _acknowledgedManualIssueKeys.Clear();
        ManualAssignments.RemoveAt(index);
        RebuildManualPreset(updateDraft: true);
    }

    public bool MoveAssignment(BlockCheck2ManualAssignmentItem item, int offset)
    {
        int oldIndex = ManualAssignments.IndexOf(item);
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= ManualAssignments.Count)
        {
            return false;
        }
        _acknowledgedManualIssueKeys.Clear();
        ManualAssignments.Move(oldIndex, newIndex);
        RebuildManualPreset(updateDraft: true);
        return true;
    }

    public void ClearManualAssignments()
    {
        _acknowledgedManualIssueKeys.Clear();
        ManualAssignments.Clear();
        RebuildManualPreset(updateDraft: true);
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

    public bool ApplyManualIssueQuickFix(BlockCheck2ManualIssueItem item)
    {
        if (!item.CanQuickFix || !ManualIssues.Contains(item))
        {
            return false;
        }

        _acknowledgedManualIssueKeys.Add(item.Key);
        ManualIssues.Remove(item);
        NotifyManualState();
        return true;
    }

    public OperationResultModel<string> GetFullStrategyArguments(
        BlockCheck2StrategyEvidenceItem item)
    {
        return _manualTestService.GetFullStrategyArguments(
            item.Evidence.Strategy,
            item.Evidence.Target,
            new BlockCheckManualStrategyTestOptions
            {
                Attempts = Math.Max(1, _session.RunOptions.Scan.AttemptsPerTarget),
            });
    }

    public async Task<BlockCheckManualStrategyTestResult> TestStrategyAsync(
        BlockCheck2StrategyEvidenceItem item,
        CancellationToken cancellationToken = default)
    {
        if (_session == null)
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

        BlockCheckManualStrategyTestResult result = await _manualTestService
            .TestWithCdpiuiAdaptersAsync(
                item.Evidence.Strategy,
                item.Evidence.Target,
                new BlockCheckManualStrategyTestOptions
                {
                    Attempts = Math.Max(1, _session.RunOptions.Scan.AttemptsPerTarget),
                },
                new CdpiuiZapret2StrategyRunnerOptions
                {
                    StartupGracePeriod = _session.StartupGracePeriod,
                },
                new CurlBlockCheckProbeRunnerOptions
                {
                    ConnectTimeout = _session.ConnectTimeout,
                    RequestTimeout = _session.RequestTimeout,
                },
                cancellationToken)
            .ConfigureAwait(true);
        if (result.ProbeResult != null)
        {
            _manualTestResults.RemoveAll(probe =>
                string.Equals(probe.StrategyId, result.ProbeResult.StrategyId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(probe.TargetId, result.ProbeResult.TargetId, StringComparison.Ordinal));
            _manualTestResults.Add(result.ProbeResult);
            item.ApplyTestResult(result.ProbeResult);
            RebuildManualPreset(updateDraft: !Draft.IsAutomatic);
        }
        return result;
    }

    private bool AddAssignment(BlockCheckManualAssignment assignment)
    {
        if (_session == null)
        {
            return false;
        }
        bool duplicate = ManualAssignments.Any(item =>
            string.Equals(item.Assignment.StrategyId, assignment.StrategyId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Assignment.AnchorTargetId, assignment.AnchorTargetId, StringComparison.Ordinal) &&
            item.Assignment.ScopeKind == assignment.ScopeKind &&
            string.Equals(item.Assignment.SiteListPath, assignment.SiteListPath, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return false;
        }

        StrategyDefinition? strategy = _session.Catalog.Strategies.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assignment.StrategyId, StringComparison.OrdinalIgnoreCase));
        BlockCheckTarget? target = _session.Targets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assignment.AnchorTargetId, StringComparison.Ordinal));
        if (strategy == null || target == null)
        {
            return false;
        }
        _acknowledgedManualIssueKeys.Clear();
        AddAssignmentCore(assignment, strategy, target);
        RebuildManualPreset(updateDraft: true);
        return true;
    }

    private void AddAssignmentCore(
        BlockCheckManualAssignment assignment,
        StrategyDefinition strategy,
        BlockCheckTarget target)
    {
        ManualAssignments.Add(new BlockCheck2ManualAssignmentItem(
            assignment,
            strategy,
            target,
            new Zapret2ConfigWriter().FormatStrategyActions(strategy)));
    }

    private void RebuildManualPreset(bool updateDraft)
    {
        ManualIssues.Clear();
        if (_session == null || ManualAssignments.Count == 0)
        {
            _manualPreset = null;
            RefreshAssignmentIndexes();
            if (updateDraft)
            {
                Draft.ApplyStructuredChange(string.Empty, []);
            }
            RefreshEvidenceSelections();
            NotifyManualState();
            return;
        }

        IEnumerable<ProbeResult> evidence = _session.RunResult.Scan.ProbeResults
            .Where(result => !_manualTestResults.Any(test =>
                string.Equals(test.StrategyId, result.StrategyId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(test.TargetId, result.TargetId, StringComparison.Ordinal)))
            .Concat(_manualTestResults);
        _manualPreset = _manualPresetService.Build(
            _session.Catalog,
            _session.Targets.Where(target => !_session.RunResult.Scan.IgnoredTargetIds.Contains(target.Id)),
            ManualAssignments.Select(item => item.Assignment),
            evidence,
            ManualOptions(_session));
        foreach (var group in _manualPreset.Issues.GroupBy(issue => new
        {
            issue.Severity,
            issue.Code,
            issue.Message,
        }))
        {
            BlockCheck2ManualIssueItem item = new(
                group.Key.Severity,
                group.Key.Code,
                group.Key.Message,
                group.Count(),
                group.Select(issue => DescribeManualIssueSubject(issue.SubjectId)));
            if (!_acknowledgedManualIssueKeys.Contains(item.Key))
            {
                ManualIssues.Add(item);
            }
        }
        if (updateDraft)
        {
            Draft.ApplyStructuredChange(
                _manualPreset.Configuration.CommandLine,
                _manualPreset.Profiles);
        }
        RefreshAssignmentIndexes();
        RefreshEvidenceSelections();
        NotifyManualState();
    }

    private string DescribeManualIssueSubject(string? subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return string.Empty;
        }

        BlockCheck2ManualAssignmentItem? scopeAssignment = ManualAssignments.FirstOrDefault(item =>
            string.Equals(BlockCheck2ManualAssignmentRules.ManualScopeKey(item), subjectId, StringComparison.OrdinalIgnoreCase));
        if (scopeAssignment != null)
        {
            return $"{scopeAssignment.Scope} · {scopeAssignment.ConnectionDetails}";
        }

        BlockCheck2ManualAssignmentItem? assignment = ManualAssignments.FirstOrDefault(item =>
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
                    BlockCheck2ManualAssignmentItem? item = ManualAssignments.FirstOrDefault(candidate =>
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

    private IEnumerable<BlockCheckTarget> FindCompatibleSiteListTargets(
        BlockCheck2StrategyEvidenceItem item)
    {
        if (_session == null)
        {
            return [];
        }

        BlockCheckTarget source = item.Evidence.Target;
        string host = BlockCheckTarget.NormalizeHost(source.Host);
        return _session.Targets.Where(target =>
            !_session.RunResult.Scan.IgnoredTargetIds.Contains(target.Id) &&
            BlockCheck2ManualAssignmentRules.IsCompatibleSiteListTarget(target, source, host));
    }

    private void SeedAutomaticAssignments(BlockCheckResultSession value)
    {
        _acknowledgedManualIssueKeys.Clear();
        ManualAssignments.Clear();
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
                    bool duplicate = ManualAssignments.Any(item => item.Assignment == assignment);
                    if (!duplicate)
                    {
                        AddAssignmentCore(assignment, strategy, target);
                        _automaticAssignmentKeys.Add(BlockCheck2ManualAssignmentRules.AssignmentKey(assignment));
                    }
                }
            }
        }

        RebuildManualPreset(updateDraft: false);
        Draft.LoadAutomatic(value.Report.PresetArguments, value.Report.Profiles);
        RefreshAssignmentIndexes();
        RefreshEvidenceSelections();
    }

    private void RefreshEvidenceSelections()
    {
        foreach (BlockCheck2StrategyEvidenceItem evidence in _allStrategyEvidence)
        {
            BlockCheck2ManualAssignmentItem[] selectedAssignments = ManualAssignments
                .Where(item => BlockCheck2ManualAssignmentRules.AssignmentMatchesEvidence(item, evidence))
                .ToArray();
            bool selected = selectedAssignments.Length > 0;
            bool automatic = selectedAssignments.Any(item =>
                _automaticAssignmentKeys.Contains(BlockCheck2ManualAssignmentRules.AssignmentKey(item.Assignment)));
            evidence.SetSelection(selected, automatic);
        }
    }

    private void RefreshAssignmentIndexes()
    {
        for (int index = 0; index < ManualAssignments.Count; index++)
        {
            ManualAssignments[index].SetOrder(index + 1);
        }
    }

    private void NotifyManualState()
    {
        Notify(nameof(HasStrategyEvidence));
        Notify(nameof(HasManualAssignments));
        Notify(nameof(CanUseConfig));
        Notify(nameof(ManualPresetArguments));
        Notify(nameof(EffectivePresetArguments));
        Notify(nameof(StrategyEvidenceCount));
        Notify(nameof(VisibleStrategyEvidenceCount));
        Notify(nameof(ManualAssignmentCount));
    }

    private static BlockCheckManualPresetOptions ManualOptions(BlockCheckResultSession session) => new()
    {
        MinimumAttempts = Math.Max(1, session.RunOptions.Synthesis.MinimumAttempts),
        MinimumSuccessRate = session.RunOptions.Synthesis.MinimumSuccessRate,
    };

    private void Notify([CallerMemberName] string? propertyName = null) => OnPropertyChanged(propertyName);
}
