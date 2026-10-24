using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Helper.AddOns.BlockCheck2;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2SiteStrategyGroup : ObservableCollection<BlockCheck2StrategyEvidenceItem>
{
    public BlockCheck2SiteStrategyGroup(string siteUrl)
    {
        SiteUrl = siteUrl;
    }

    public string SiteUrl { get; }
}

public sealed class BlockCheck2ManualIssueItem
{
    public BlockCheck2ManualIssueItem(
        BlockCheckIssueSeverity severity,
        string code,
        string message,
        int count,
        IEnumerable<string> subjects)
    {
        Severity = severity;
        Code = code;
        Message = BlockCheck2IssueText.GetMessage(code, message);
        Count = Math.Max(1, count);
        string[] materializedSubjects = subjects
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(subject => subject, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        SubjectText = string.Join(", ", materializedSubjects);
        Key = $"{Severity}|{Code}|{message}|{string.Join('|', materializedSubjects)}";
    }

    public BlockCheckIssueSeverity Severity { get; }
    public string Code { get; }
    public string DisplayCode => Count > 1 ? $"{Code} ×{Count}" : Code;
    public string Message { get; }
    public int Count { get; }
    public string SubjectText { get; }
    public bool HasSubjects => SubjectText.Length > 0;
    public string Key { get; }
    public bool CanQuickFix => string.Equals(
        Code,
        "MANUAL_SCOPE_OVERLAP",
        StringComparison.OrdinalIgnoreCase);
}

public sealed class BlockCheck2StrategyEvidenceItem : INotifyPropertyChanged
{
    private ProbeSummary _summary;
    private BlockCheckEvidenceStatus _status;
    private string _httpStatusText;
    private string _failureText;
    private bool _isSelectedForPreset;
    private bool _isAutomaticSelection;

    public BlockCheck2StrategyEvidenceItem(BlockCheckStrategyEvidence evidence)
    {
        Evidence = evidence;
        _summary = evidence.Summary;
        _status = evidence.Status;
        SiteUrl = BlockCheckTargetDisplayFormatter.FormatUrl(evidence.Target);
        ConnectionDetails = BlockCheckTargetDisplayFormatter.FormatConnectionDetails(evidence.Target);
        StrategyName = string.IsNullOrWhiteSpace(evidence.Strategy.DisplayName)
            ? evidence.Strategy.Id
            : evidence.Strategy.DisplayName;
        _httpStatusText = FormatStatuses(evidence.HttpStatusCodes);
        _failureText = string.Join(", ", evidence.FailureCodes);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BlockCheckStrategyEvidence Evidence { get; }
    public string TargetId => Evidence.Target.Id;
    public string StrategyId => Evidence.Strategy.Id;
    public string StrategyName { get; }
    public string StrategyArguments => Evidence.StrategyArguments;
    public string SiteUrl { get; }
    public string ConnectionDetails { get; }
    public BlockCheckEvidenceStatus Status => _status;
    public int AttemptCount => _summary.AttemptCount;
    public int SuccessCount => _summary.SuccessCount;
    public double SuccessRate => _summary.SuccessRate;
    public double ResponseTimeSortValue =>
        double.IsFinite(_summary.MedianTimeToFirstByteMs)
            ? _summary.MedianTimeToFirstByteMs
            : double.PositiveInfinity;
    public string ResultText => $"{SuccessCount}/{AttemptCount} ({SuccessRate:P0})";
    public bool HasPartialScopeWarning =>
        _status == BlockCheckEvidenceStatus.Partial && SuccessCount == AttemptCount;
    public string ResponseTimeText =>
        double.IsFinite(_summary.MedianTimeToFirstByteMs)
            ? $"{_summary.MedianTimeToFirstByteMs:0} ms"
            : "—";
    public string HttpStatusText => _httpStatusText;
    public string FailureText => _failureText;
    public string BaselineText =>
        $"{Evidence.BaselineSummary.SuccessCount}/{Evidence.BaselineSummary.AttemptCount} " +
        $"({Evidence.BaselineSummary.SuccessRate:P0})";
    public string EvidenceDetails => string.Join(" · ", new[]
    {
        ResultText,
        ResponseTimeText,
        string.IsNullOrWhiteSpace(HttpStatusText) ? null : $"HTTP {HttpStatusText}",
        string.IsNullOrWhiteSpace(FailureText) ? null : FailureText,
    }.Where(value => value != null));

    public bool IsSelectedForPreset => _isSelectedForPreset;
    public bool IsAutomaticSelection => _isAutomaticSelection;
    public string SelectionText => !_isSelectedForPreset
        ? string.Empty
        : _isAutomaticSelection
            ? "Auto"
            : "Manual";

    public void SetSelection(bool selected, bool automatic)
    {
        if (_isSelectedForPreset == selected && _isAutomaticSelection == automatic)
        {
            return;
        }
        _isSelectedForPreset = selected;
        _isAutomaticSelection = automatic;
        Notify(nameof(IsSelectedForPreset));
        Notify(nameof(IsAutomaticSelection));
        Notify(nameof(SelectionText));
    }

    public void ApplyTestResult(ProbeResult result)
    {
        ProbeAttempt[] attempts = result.Attempts.ToArray();
        _summary = ProbeSummary.FromAttempts(attempts);
        _status = _summary.AttemptCount == 0
            ? BlockCheckEvidenceStatus.Untested
            : _summary.SuccessCount == _summary.AttemptCount
                ? BlockCheckEvidenceStatus.Successful
                : _summary.SuccessCount > 0
                    ? BlockCheckEvidenceStatus.Partial
                    : BlockCheckEvidenceStatus.Failed;
        _httpStatusText = FormatStatuses(attempts
            .Select(attempt => attempt.HttpStatusCode)
            .Where(code => code > 0)
            .Distinct()
            .Order());
        _failureText = string.Join(", ", attempts
            .Select(attempt => attempt.FailureCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        Notify(nameof(Status));
        Notify(nameof(AttemptCount));
        Notify(nameof(SuccessCount));
        Notify(nameof(SuccessRate));
        Notify(nameof(ResponseTimeSortValue));
        Notify(nameof(ResultText));
        Notify(nameof(HasPartialScopeWarning));
        Notify(nameof(ResponseTimeText));
        Notify(nameof(HttpStatusText));
        Notify(nameof(FailureText));
        Notify(nameof(EvidenceDetails));
    }

    private static string FormatStatuses(System.Collections.Generic.IEnumerable<int> statuses) =>
        string.Join("/", statuses);

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class BlockCheck2ManualAssignmentItem : INotifyPropertyChanged
{
    private int _order;

    public BlockCheck2ManualAssignmentItem(
        BlockCheckManualAssignment assignment,
        StrategyDefinition strategy,
        BlockCheckTarget target,
        string strategyArguments)
    {
        Assignment = assignment;
        Strategy = strategy;
        Target = target;
        StrategyArguments = strategyArguments;
        Scope = assignment.ScopeKind == BlockCheckManualScopeKind.Site
            ? BlockCheckTargetDisplayFormatter.FormatUrl(target)
            : Path.GetFileName(assignment.SiteListPath) ?? assignment.SiteListPath ?? string.Empty;
        ConnectionDetails = BlockCheckTargetDisplayFormatter.FormatConnectionDetails(target);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BlockCheckManualAssignment Assignment { get; }
    public StrategyDefinition Strategy { get; }
    public BlockCheckTarget Target { get; }
    public int Order => _order;
    public string Scope { get; }
    public string ConnectionDetails { get; }
    public string StrategyArguments { get; }
    public string StrategyName => string.IsNullOrWhiteSpace(Strategy.DisplayName)
        ? Strategy.Id
        : Strategy.DisplayName;
    public string ScopeKind => Assignment.ScopeKind.ToString();

    public void SetOrder(int value)
    {
        if (_order == value)
        {
            return;
        }
        _order = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Order)));
    }
}
