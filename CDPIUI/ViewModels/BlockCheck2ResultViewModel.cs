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

public sealed class BlockCheck2ResultViewModel : ObservableObject, IBlockCheck2ManualPresetHost
{
    private readonly BlockCheckManualPresetService _manualPresetService = new();
    private readonly BlockCheckManualStrategyTestService _manualTestService = new();
    private BlockCheckReport? _report;
    private BlockCheckResultSession? _session;
    private IReadOnlyList<BlockCheck2StrategyEvidenceItem> _allStrategyEvidence = [];
    private readonly List<ProbeResult> _manualTestResults = [];
    private readonly BlockCheck2ManualAssignmentEditor _assignmentEditor;
    private readonly HashSet<string> _acknowledgedManualIssueKeys = new(StringComparer.Ordinal);
    private readonly BlockCheck2ManualPresetState _manualPresetState;
    private readonly BlockCheck2ManualStrategyTestController _manualStrategyTest;

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
    public string ManualPresetArguments => _manualPresetState.Arguments;
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
        _manualPresetState = new BlockCheck2ManualPresetState(Draft, _manualTestResults, this,
            new BlockCheck2ManualPresetOperations(_manualPresetService));
        _manualStrategyTest = new BlockCheck2ManualStrategyTestController(Draft, _manualTestResults,
            () => _session, RebuildManualPreset, new BlockCheck2ManualStrategyTestOperations(_manualTestService));
        _assignmentEditor = new BlockCheck2ManualAssignmentEditor(Draft,
            () => _session, () => _allStrategyEvidence,
            () => _acknowledgedManualIssueKeys.Clear(), RebuildManualPreset);
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
        _assignmentEditor.ClearAutomaticKeys();
        _acknowledgedManualIssueKeys.Clear();
        StrategyEvidence.Clear();
        StrategyGroups.Clear();
        ManualAssignments.Clear();
        ManualIssues.Clear();
        _manualPresetState.ClearResult();
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

    public bool AddForSite(BlockCheck2StrategyEvidenceItem item) => _assignmentEditor.AddForSite(item);

    public bool AddForSiteList(BlockCheck2StrategyEvidenceItem item, string siteListPath) =>
        _assignmentEditor.AddForSiteList(item, siteListPath);

    public void RemoveAssignment(BlockCheck2ManualAssignmentItem item) => _assignmentEditor.RemoveAssignment(item);

    public bool MoveAssignment(BlockCheck2ManualAssignmentItem item, int offset) => _assignmentEditor.MoveAssignment(item, offset);

    public void ClearManualAssignments() => _assignmentEditor.ClearManualAssignments();

    public IReadOnlyList<string> GetSiteListPaths(BlockCheck2StrategyEvidenceItem item) => _assignmentEditor.GetSiteListPaths(item);

    public bool CanAddForSiteList(BlockCheck2StrategyEvidenceItem item) => _assignmentEditor.CanAddForSiteList(item);

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
        BlockCheck2StrategyEvidenceItem item) => _manualStrategyTest.GetFullStrategyArguments(item);

    public Task<BlockCheckManualStrategyTestResult> TestStrategyAsync(
        BlockCheck2StrategyEvidenceItem item,
        CancellationToken cancellationToken = default) => _manualStrategyTest.TestStrategyAsync(item, cancellationToken);

    private void RebuildManualPreset(bool updateDraft) => _manualPresetState.Rebuild(updateDraft);

    BlockCheckResultSession? IBlockCheck2ManualPresetHost.Session => _session;
    void IBlockCheck2ManualPresetHost.ClearIssues() => ManualIssues.Clear();
    void IBlockCheck2ManualPresetHost.RefreshAssignmentIndexes() => RefreshAssignmentIndexes();
    void IBlockCheck2ManualPresetHost.RefreshEvidenceSelections() => RefreshEvidenceSelections();
    void IBlockCheck2ManualPresetHost.NotifyManualState() => NotifyManualState();
    void IBlockCheck2ManualPresetHost.AddIssueGroup(BlockCheckIssueSeverity severity, string code,
        string message, int count, IEnumerable<string> subjects)
    {
        BlockCheck2ManualIssueItem item = new(severity, code, message, count, subjects);
        if (!_acknowledgedManualIssueKeys.Contains(item.Key))
        {
            ManualIssues.Add(item);
        }
    }

    private void SeedAutomaticAssignments(BlockCheckResultSession value) => _assignmentEditor.SeedAutomaticAssignments(value);

    private void RefreshEvidenceSelections() => _assignmentEditor.RefreshEvidenceSelections();

    private void RefreshAssignmentIndexes() => _assignmentEditor.RefreshAssignmentIndexes();

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

    private static BlockCheckManualPresetOptions ManualOptions(BlockCheckResultSession session) =>
        BlockCheck2ManualPresetState.OptionsFor(session);

    private void Notify([CallerMemberName] string? propertyName = null) => OnPropertyChanged(propertyName);
}
