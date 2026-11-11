#nullable enable

using System;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.ViewModels.BlockCheck2;

// Owns progress display values; numeric progress and run lifecycle stay with Session.
internal sealed class BlockCheck2ProgressPresentation : ObservableObject
{
    private readonly Func<BlockCheckScanPhase?> _phase;
    private readonly Func<bool> _running;
    private readonly Func<int> _current, _total;
    private readonly Func<TimeSpan?> _stageEstimate, _overallEstimate;
    private readonly Func<string, string> _text;
    private readonly Func<TimeSpan?, string, string> _formatRemaining;
    private string _progressPhaseText = string.Empty;
    private string _progressCounterText = string.Empty;
    private string _stageRemainingTimeText = string.Empty;
    private string _overallRemainingTimeText = string.Empty;
    private bool _isAdditionalProgressInfoVisible;
    private string? _progressViewMoreText;

    public BlockCheck2ProgressPresentation(Func<BlockCheckScanPhase?> phase, Func<bool> running,
        Func<int> current, Func<int> total, Func<TimeSpan?> stageEstimate, Func<TimeSpan?> overallEstimate,
        Func<string, string> text, Func<TimeSpan?, string, string> formatRemaining)
    {
        _phase = phase;
        _running = running;
        _current = current;
        _total = total;
        _stageEstimate = stageEstimate;
        _overallEstimate = overallEstimate;
        _text = text;
        _formatRemaining = formatRemaining;
    }

    public string ProgressPhaseText
    {
        get => _progressPhaseText;
        set => SetProperty(ref _progressPhaseText, value);
    }

    public string ProgressCounterText
    {
        get => _progressCounterText;
        set => SetProperty(ref _progressCounterText, value);
    }

    public string StageRemainingTimeText
    {
        get => _stageRemainingTimeText;
        set => SetProperty(ref _stageRemainingTimeText, value);
    }

    public string OverallRemainingTimeText
    {
        get => _overallRemainingTimeText;
        set => SetProperty(ref _overallRemainingTimeText, value);
    }

    public bool IsAdditionalProgressInfoVisible
    {
        get => _isAdditionalProgressInfoVisible;
        set => SetProperty(ref _isAdditionalProgressInfoVisible, value);
    }

    public string? ProgressViewMoreText
    {
        get => _progressViewMoreText;
        set => SetProperty(ref _progressViewMoreText, value);
    }

    public void ToggleAdditionalProgressInfo()
    {
        IsAdditionalProgressInfoVisible = !IsAdditionalProgressInfoVisible;
        UpdateProgressViewMoreText();
    }

    public void UpdateProgressViewMoreText() =>
        ProgressViewMoreText = _text(IsAdditionalProgressInfoVisible ? "ViewLess" : "ViewMore");

    public void UpdateProgressText()
    {
        ProgressPhaseText = _phase() switch
        {
            BlockCheckScanPhase.BaselineProbing => _text("BlockCheck2PhaseBaseline"),
            BlockCheckScanPhase.StartingStrategy => _text("BlockCheck2PhaseStartingStrategy"),
            BlockCheckScanPhase.Probing => _text("BlockCheck2PhaseProbing"),
            BlockCheckScanPhase.StrategyCompleted => _text("BlockCheck2PhaseStrategyCompleted"),
            BlockCheckScanPhase.ValidatingPreset => _text("BlockCheck2PhaseValidatingConfig"),
            BlockCheckScanPhase.RepairingPreset => _text("BlockCheck2PhaseRepairingConfig"),
            _ => _text("BlockCheck2PhasePreparing"),
        };
        ProgressCounterText = string.Format(
            _text("BlockCheck2ProgressCounterFormat"),
            _current(),
            _total());
        StageRemainingTimeText = FormatRemainingTime();
        OverallRemainingTimeText = FormatOverallRemainingTime();
    }

    public string FormatRemainingTime()
    {
        if (!_running() && _phase().HasValue)
        {
            return "—";
        }

        return _formatRemaining(_stageEstimate(), "BlockCheck2RemainingTimeCalculating");
    }

    public string FormatOverallRemainingTime()
    {
        if (!_running() && _phase().HasValue)
        {
            return "—";
        }

        return _formatRemaining(
            _overallEstimate(),
            _phase() == BlockCheckScanPhase.BaselineProbing
                ? "BlockCheck2OverallTimeCalculating"
                : "BlockCheck2RemainingTimeCalculating");
    }
}
