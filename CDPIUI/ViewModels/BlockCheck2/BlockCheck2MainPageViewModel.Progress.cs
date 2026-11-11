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
        _completionPresenter.Show();

        UpdateNavigationButtons();
    }
}
