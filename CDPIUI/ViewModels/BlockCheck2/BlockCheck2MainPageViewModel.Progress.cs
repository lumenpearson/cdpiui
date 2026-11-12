using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2MainPageViewModel
{
    public string ProgressPhaseText
    {
        get => _progressPresentation.ProgressPhaseText;
        private set => _progressPresentation.ProgressPhaseText = value;
    }

    public string ProgressCounterText
    {
        get => _progressPresentation.ProgressCounterText;
        private set => _progressPresentation.ProgressCounterText = value;
    }

    public string StageRemainingTimeText
    {
        get => _progressPresentation.StageRemainingTimeText;
        private set => _progressPresentation.StageRemainingTimeText = value;
    }

    public string OverallRemainingTimeText
    {
        get => _progressPresentation.OverallRemainingTimeText;
        private set => _progressPresentation.OverallRemainingTimeText = value;
    }

    public bool IsAdditionalProgressInfoVisible
    {
        get => _progressPresentation.IsAdditionalProgressInfoVisible;
        private set => _progressPresentation.IsAdditionalProgressInfoVisible = value;
    }

    public string? ProgressViewMoreText
    {
        get => _progressPresentation.ProgressViewMoreText;
        private set => _progressPresentation.ProgressViewMoreText = value;
    }

    public void ToggleAdditionalProgressInfo() => _progressPresentation.ToggleAdditionalProgressInfo();

    private void UpdateProgressViewMoreText() =>
        _progressPresentation.UpdateProgressViewMoreText();

    private void UpdateProgressText() => _progressPresentation.UpdateProgressText();

    private string FormatRemainingTime() => _progressPresentation.FormatRemainingTime();

    private string FormatOverallRemainingTime() => _progressPresentation.FormatOverallRemainingTime();

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
