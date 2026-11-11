#nullable enable

using System;

namespace CDPIUI.ViewModels.BlockCheck2;

internal sealed class BlockCheck2WizardNavigation : ObservableObject
{
    private readonly IBlockCheck2NavigationSource _source;
    private readonly IBlockCheck2MainPageView _view;
    private readonly Func<string, string> _text;
    private readonly Action _notifyCancelChanged;
    private BlockCheck2WizardStep _currentStep = BlockCheck2WizardStep.SiteLists;
    private bool _isNextEnabled = true;
    private string? _nextButtonText;
    private bool _isCancelEnabled = true;

    public BlockCheck2WizardNavigation(IBlockCheck2NavigationSource source,
        IBlockCheck2MainPageView view, Func<string, string> text, Action notifyCancelChanged)
    {
        _source = source;
        _view = view;
        _text = text;
        _notifyCancelChanged = notifyCancelChanged;
    }

    public BlockCheck2WizardStep CurrentStep => _currentStep;
    public bool IsCancelEnabled => _isCancelEnabled;
    public bool IsNextEnabled
    {
        get => _isNextEnabled;
        set => SetProperty(ref _isNextEnabled, value);
    }

    public string? NextButtonText
    {
        get => _nextButtonText;
        set => SetProperty(ref _nextButtonText, value);
    }

    public void GoToStep(BlockCheck2WizardStep step)
    {
        _currentStep = step;
        _view.GoToStep(step);
    }

    public void SetCancelEnabled(bool value)
    {
        _isCancelEnabled = value;
        _notifyCancelChanged();
    }

    public void UpdateNavigationButtons()
    {
        bool running = _source.IsRunning;
        bool onSiteLists = _currentStep == BlockCheck2WizardStep.SiteLists;
        bool onMode = _currentStep == BlockCheck2WizardStep.Mode;
        bool onAdvanced = _currentStep == BlockCheck2WizardStep.Advanced;
        bool onReview = _currentStep == BlockCheck2WizardStep.Review;
        bool onProgress = _currentStep == BlockCheck2WizardStep.Progress;
        bool onComplete = _currentStep == BlockCheck2WizardStep.WorkComplete;

        _view.ShowNavigationButtons(new BlockCheck2NavigationButtons(
            Exit: onComplete,
            Back: !running,
            Cancel: running,
            ViewResult: !running && onComplete && _source.HasReport,
            Next: !running && !onProgress && !onComplete));

        SetCancelEnabled(_source.CanCancel);
        IsNextEnabled = onSiteLists
            ? _source.HasTargetSources
            : onAdvanced
                ? _source.AdvancedOptionsValid && _source.CanContinueFromTargets
                : onMode || (onReview && _source.CanStart);
        NextButtonText = onReview
            ? _text("BlockCheck2StartButton")
            : _text("BlockCheck2NextButton");
    }
}
