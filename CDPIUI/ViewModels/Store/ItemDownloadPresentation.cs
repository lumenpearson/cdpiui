#nullable enable

using System;

namespace CDPIUI.ViewModels.Store;

internal sealed class ItemDownloadPresentation : ObservableObject
{
    private readonly Func<string, string> _text;
    private bool _errorHappens;
    private bool _isActionVisible = true;
    private bool _isDownloadStatusVisible;
    private bool _isErrorStatusVisible;
    private bool _isStopEnabled = true;
    private string? _statusText = string.Empty;
    private bool _isSpeedVisible;
    private string? _speedText = "";
    private string? _statusTip;
    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private string? _errorName = "ERR_FLASHLIGHT_BISY";

    public ItemDownloadPresentation(Func<string, string> text)
    {
        _text = text;
    }

    public bool ErrorHappens
    {
        get => _errorHappens;
        set => _errorHappens = value;
    }

    public bool IsActionVisible
    {
        get => _isActionVisible;
        set => SetProperty(ref _isActionVisible, value);
    }

    public bool IsDownloadStatusVisible
    {
        get => _isDownloadStatusVisible;
        set => SetProperty(ref _isDownloadStatusVisible, value);
    }

    public bool IsErrorStatusVisible
    {
        get => _isErrorStatusVisible;
        set => SetProperty(ref _isErrorStatusVisible, value);
    }

    public bool IsStopEnabled
    {
        get => _isStopEnabled;
        set => SetProperty(ref _isStopEnabled, value);
    }

    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsSpeedVisible
    {
        get => _isSpeedVisible;
        set => SetProperty(ref _isSpeedVisible, value);
    }

    public string? SpeedText
    {
        get => _speedText;
        set => SetProperty(ref _speedText, value);
    }

    public string? StatusTip
    {
        get => _statusTip;
        set
        {
            _statusTip = value;
            OnPropertyChanged();
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string? ErrorName
    {
        get => _errorName;
        set => SetProperty(ref _errorName, value);
    }

    public void ShowDownloadState()
    {
        IsStopEnabled = true;
        IsProgressIndeterminate = true;
        IsErrorStatusVisible = false;
        IsActionVisible = false;
        IsDownloadStatusVisible = true;
    }

    public void ApplyStage(string? state)
    {
        state ??= "QueueWaiting";
        string stageHeaderText = string.Empty;
        switch (state)
        {
            case "GETR":
                stageHeaderText = _text("GettingReady");
                IsProgressIndeterminate = true;
                break;
            case "END":
                stageHeaderText = _text("Finishing");
                IsProgressIndeterminate = true;
                break;
            case "Downloading":
                stageHeaderText = _text("Downloading");
                IsProgressIndeterminate = false;
                IsSpeedVisible = true;
                break;
            case "Extracting":
                stageHeaderText = _text("Installing");
                IsProgressIndeterminate = true;
                break;
            case "ErrorHappens":
                stageHeaderText = _text("ErrorHappens");
                break;
            case "Completed":
                stageHeaderText = _text("Finishing");
                IsProgressIndeterminate = true;
                break;
            case "CANC":
                stageHeaderText = _text("Cancel");
                IsProgressIndeterminate = true;
                break;
            case "ConnectingToService":
                stageHeaderText = _text("ConnectingToService");
                StatusTip = _text("ConnectingToServiceTip");
                IsProgressIndeterminate = true;
                break;
            default:
                stageHeaderText = _text(state);
                IsProgressIndeterminate = true;
                break;
        }
        StatusText = stageHeaderText;
    }

    public void OnStageChanged(string? stage)
    {
        if (_errorHappens)
            return;

        ShowDownloadState();

        IsSpeedVisible = false;

        ApplyStage(stage);
    }

    public void OnProgressChanged(double progress)
    {
        if (_errorHappens)
            return;

        IsProgressIndeterminate = false;
        ProgressValue = progress;
    }

    public void OnTimeRemainingChanged(TimeSpan time)
    {
        if (_errorHappens)
            return;

        if (time.TotalMinutes >= 1)
            StatusTip = string.Format(_text("/UIHelper/LeftMinutes"), (int)time.TotalMinutes);
        else
            StatusTip = _text("/UIHelper/LeftSmall");
    }

    public void OnSpeedChanged(string? speedText)
    {
        if (_errorHappens)
            return;

        SpeedText = speedText;
    }

    public void OnInstallingError(string? errorCode)
    {
        IsErrorStatusVisible = true;
        IsActionVisible = false;
        IsDownloadStatusVisible = false;

        ErrorName = errorCode;
        _errorHappens = true;
    }
}
