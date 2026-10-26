using System;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2MainPageViewModel
{
    private string _progressPhaseText = string.Empty;
    private string _progressCounterText = string.Empty;
    private string _stageRemainingTimeText = string.Empty;
    private string _overallRemainingTimeText = string.Empty;
    private bool _isAdditionalProgressInfoVisible;
    private string? _progressViewMoreText;

    public string ProgressPhaseText
    {
        get => _progressPhaseText;
        private set => SetProperty(ref _progressPhaseText, value);
    }

    public string ProgressCounterText
    {
        get => _progressCounterText;
        private set => SetProperty(ref _progressCounterText, value);
    }

    public string StageRemainingTimeText
    {
        get => _stageRemainingTimeText;
        private set => SetProperty(ref _stageRemainingTimeText, value);
    }

    public string OverallRemainingTimeText
    {
        get => _overallRemainingTimeText;
        private set => SetProperty(ref _overallRemainingTimeText, value);
    }

    public bool IsAdditionalProgressInfoVisible
    {
        get => _isAdditionalProgressInfoVisible;
        private set => SetProperty(ref _isAdditionalProgressInfoVisible, value);
    }

    public string? ProgressViewMoreText
    {
        get => _progressViewMoreText;
        private set => SetProperty(ref _progressViewMoreText, value);
    }

    public void ToggleAdditionalProgressInfo()
    {
        IsAdditionalProgressInfoVisible = !IsAdditionalProgressInfoVisible;
        UpdateProgressViewMoreText();
    }

    private void UpdateProgressViewMoreText() =>
        ProgressViewMoreText = Text(IsAdditionalProgressInfoVisible ? "ViewLess" : "ViewMore");

    private void UpdateProgressText()
    {
        ProgressPhaseText = Session.CurrentPhase switch
        {
            BlockCheckScanPhase.BaselineProbing => Text("BlockCheck2PhaseBaseline"),
            BlockCheckScanPhase.StartingStrategy => Text("BlockCheck2PhaseStartingStrategy"),
            BlockCheckScanPhase.Probing => Text("BlockCheck2PhaseProbing"),
            BlockCheckScanPhase.StrategyCompleted => Text("BlockCheck2PhaseStrategyCompleted"),
            BlockCheckScanPhase.ValidatingPreset => Text("BlockCheck2PhaseValidatingConfig"),
            BlockCheckScanPhase.RepairingPreset => Text("BlockCheck2PhaseRepairingConfig"),
            _ => Text("BlockCheck2PhasePreparing"),
        };
        ProgressCounterText = string.Format(
            Text("BlockCheck2ProgressCounterFormat"),
            Session.ProgressCurrent,
            Session.ProgressTotal);
        StageRemainingTimeText = FormatRemainingTime();
        OverallRemainingTimeText = FormatOverallRemainingTime();
    }

    private string FormatRemainingTime()
    {
        if (!Session.IsRunning && Session.CurrentPhase.HasValue)
        {
            return "—";
        }

        return _durationText.FormatRemaining(
            Session.EstimatedRemainingTime,
            "BlockCheck2RemainingTimeCalculating");
    }

    private string FormatOverallRemainingTime()
    {
        if (!Session.IsRunning && Session.CurrentPhase.HasValue)
        {
            return "—";
        }

        return _durationText.FormatRemaining(
            Session.EstimatedOverallRemainingTime,
            Session.CurrentPhase == BlockCheckScanPhase.BaselineProbing
                ? "BlockCheck2OverallTimeCalculating"
                : "BlockCheck2RemainingTimeCalculating");
    }

    private void HandleRunCompleted()
    {
        if (Session.State == BlockCheckSessionState.InputInvalid)
        {
            GoToStep(BlockCheck2WizardStep.SiteLists);
            ShowInputIssue(FormatIssues());
            UpdateNavigationButtons();
            return;
        }

        GoToStep(BlockCheck2WizardStep.WorkComplete);

        IsResultNoticeOpen = true;
        BlockCheck2NoticeSeverity severity;
        string title;
        string message;
        if (Session.State == BlockCheckSessionState.Completed && Session.NoBypassRequired)
        {
            severity = BlockCheck2NoticeSeverity.Informational;
            title = Text("BlockCheck2NoBypassRequiredTitle");
            message = Text(Session.LastReportIsHttpOnly
                ? "BlockCheck2HttpOnlyNoBypassRequiredMessage"
                : "BlockCheck2NoBypassRequiredMessage");
        }
        else if (Session.State == BlockCheckSessionState.Completed)
        {
            severity = BlockCheck2NoticeSeverity.Success;
            title = Text("BlockCheck2CompletedTitle");
            message = Text("BlockCheck2CompletedMessage");
        }
        else if (Session.State == BlockCheckSessionState.CompletedWithWarnings)
        {
            severity = BlockCheck2NoticeSeverity.Warning;
            title = Text("BlockCheck2BestEffortCompletedTitle");
            message = Text("BlockCheck2BestEffortCompletedMessage");
        }
        else if (Session.State == BlockCheckSessionState.Canceled)
        {
            severity = BlockCheck2NoticeSeverity.Informational;
            title = Text("BlockCheck2CanceledTitle");
            message = Text("BlockCheck2CanceledMessage");
        }
        else
        {
            severity = BlockCheck2NoticeSeverity.Error;
            title = Text("BlockCheck2FailedTitle");
            message = string.IsNullOrWhiteSpace(Session.OperationError)
                ? FormatIssues()
                : Session.OperationError;
        }

        if (!string.IsNullOrWhiteSpace(Session.HistorySaveError))
        {
            message = string.Concat(
                message,
                Environment.NewLine,
                Text("BlockCheck2HistorySaveFailedMessage"),
                " ",
                Session.HistorySaveError);
        }

        ResultNoticeSeverity = severity;
        ResultNoticeTitle = title;
        ResultNoticeMessage = message;

        UpdateNavigationButtons();
    }
}
