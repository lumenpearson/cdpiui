using System;
using System.Linq;
using CDPIUI.Core.Store;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.PrettyErrorConversionService;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Store;

/// <summary>
/// State of one row of the Store downloads list: the queue stage, progress, speed, time and
/// error of the item, and the layout of a download row or an update row. The Store events are
/// handled on the thread that raises them, as the control did before.
/// </summary>
public sealed partial class StoreDownloadItemButtonViewModel : ObservableObject
{
    public const string CancelGlyph = "";
    public const string RetryGlyph = "";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly Action _executeClickCommand;

    private string _statusText;
    private string _speedText = string.Empty;
    private string _timeText = string.Empty;
    private string _errorText = string.Empty;
    private string _actionGlyph = CancelGlyph;
    private string _tooltip;
    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private bool _isProgressVisible;
    private bool _isProgressCritical;
    private bool _isStatusVisible = true;
    private bool _isCancelVisible = true;
    private bool _isDescriptionVisible;
    private bool _isUpdateAreaVisible = true;
    private bool _isVersionVisible;

    /// <param name="executeClickCommand">Runs the click command of the control.</param>
    public StoreDownloadItemButtonViewModel(Action executeClickCommand)
    {
        _executeClickCommand = executeClickCommand;
        _statusText = _localizer.GetLocalizedString("QueueWaiting");
        _tooltip = _localizer.GetLocalizedString("Cancel");
        CancelCommand = new RelayCommand(Cancel);
    }

    public RelayCommand CancelCommand { get; }

    /// <summary>The Store id of the item; the control keeps it equal to its <c>StoreId</c> property.</summary>
    public string StoreId { get; set; } = string.Empty;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
    }

    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public string ActionGlyph
    {
        get => _actionGlyph;
        private set => SetProperty(ref _actionGlyph, value);
    }

    public string Tooltip
    {
        get => _tooltip;
        private set => SetProperty(ref _tooltip, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        private set => SetProperty(ref _isProgressVisible, value);
    }

    /// <summary>
    /// <see langword="true"/> when the progress bar shows an error. The change is raised each
    /// time the error state is applied or cleared, so the view sets the bar color each time.
    /// </summary>
    public bool IsProgressCritical => _isProgressCritical;

    public bool IsStatusVisible
    {
        get => _isStatusVisible;
        private set => SetProperty(ref _isStatusVisible, value);
    }

    public bool IsCancelVisible
    {
        get => _isCancelVisible;
        private set => SetProperty(ref _isCancelVisible, value);
    }

    public bool IsDescriptionVisible
    {
        get => _isDescriptionVisible;
        private set => SetProperty(ref _isDescriptionVisible, value);
    }

    public bool IsUpdateAreaVisible
    {
        get => _isUpdateAreaVisible;
        private set => SetProperty(ref _isUpdateAreaVisible, value);
    }

    public bool IsVersionVisible
    {
        get => _isVersionVisible;
        private set => SetProperty(ref _isVersionVisible, value);
    }

    /// <summary>Called after the control sets its <c>StoreId</c> property.</summary>
    public void OnStoreIdSet(bool isErrorHappens)
    {
        if (isErrorHappens)
            ApplyError(GetStoredErrorCode());

        Disconnect();
        Connect();
        CheckCurrentStatus();
    }

    /// <summary>Called after the control sets its <c>IsUpdateLikeButton</c> property.</summary>
    public void SetUpdateLike(bool isUpdateLike, string operationId)
    {
        if (isUpdateLike)
        {
            IsStatusVisible = false;
            IsProgressVisible = false;
            IsCancelVisible = false;
            IsDescriptionVisible = true;
            IsUpdateAreaVisible = true;
            IsVersionVisible = true;
        }
        else
        {
            IsStatusVisible = true;
            IsCancelVisible = true;

            if (StoreHelper.Instance.GetCurrentQueueOperationId() == operationId)
                IsProgressVisible = true;

            IsDescriptionVisible = false;
            IsUpdateAreaVisible = false;
            IsVersionVisible = false;
        }
    }

    /// <summary>Called after the control sets its <c>IsErrorHappens</c> property.</summary>
    public void SetErrorHappens(bool isErrorHappens)
    {
        if (isErrorHappens)
            ApplyError(GetStoredErrorCode());
        else
            ClearError();
    }

    private string GetStoredErrorCode() =>
        StoreHelper.Instance.GetFailedToInstallItems().FirstOrDefault(x => x.ItemId == StoreId)?.ErrorCode ?? string.Empty;

    private void Cancel()
    {
        if (!StoreHelper.Instance.RemoveItemFromQueue(StoreId))
        {
            StoreHelper.Instance.AddItemToQueue(StoreId, string.Empty);
            _executeClickCommand();
        }
    }

    private void CheckCurrentStatus()
    {
        string operationId = StoreHelper.Instance.GetOperationIdFromItemId(StoreId);
        if (!string.IsNullOrEmpty(operationId))
        {
            string status = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadStage;
            ApplyStage(status);
        }
    }

    private void Connect()
    {
        StoreHelper.Instance.ItemDownloadStageChanged += StoreHelper_ItemDownloadStageChanged;
        StoreHelper.Instance.ItemDownloadProgressChanged += StoreHelper_ItemDownloadProgressChanged;
        StoreHelper.Instance.ItemTimeRemainingChanged += StoreHelper_ItemTimeRemainingChanged;
        StoreHelper.Instance.ItemDownloadSpeedChanged += StoreHelper_ItemDownloadSpeedChanged;
        StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
        StoreHelper.Instance.ItemInstallingErrorHappens += StoreHelper_ItemInstallingErrorHappens;
    }

    private void Disconnect()
    {
        StoreHelper.Instance.ItemDownloadStageChanged -= StoreHelper_ItemDownloadStageChanged;
        StoreHelper.Instance.ItemDownloadProgressChanged -= StoreHelper_ItemDownloadProgressChanged;
        StoreHelper.Instance.ItemTimeRemainingChanged -= StoreHelper_ItemTimeRemainingChanged;
        StoreHelper.Instance.ItemDownloadSpeedChanged -= StoreHelper_ItemDownloadSpeedChanged;
        StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;
        StoreHelper.Instance.ItemInstallingErrorHappens -= StoreHelper_ItemInstallingErrorHappens;
    }

    private void StoreHelper_ItemInstallingErrorHappens(Tuple<string, ErrorModel> obj)
    {
        string operationId = obj.Item1;
        string errorCode = obj.Item2.ErrorCode;

        if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
            return;

        ApplyError(errorCode);
    }

    private void ClearError()
    {
        ErrorText = "";
        ActionGlyph = CancelGlyph;
        StatusText = _localizer.GetLocalizedString("QueueWaiting");

        IsProgressIndeterminate = true;
        ProgressValue = 0;

        Tooltip = _localizer.GetLocalizedString("Cancel");

        _isProgressCritical = false;
        OnPropertyChanged(nameof(IsProgressCritical));
        CheckCurrentStatus();
    }

    private void ApplyError(string errorCode)
    {
        ActionGlyph = RetryGlyph;
        ErrorText = errorCode;
        SpeedText = "";
        TimeText = "";
        StatusText = _localizer.GetLocalizedString("ErrorHappens");

        IsProgressIndeterminate = false;
        ProgressValue = 100;

        Tooltip = _localizer.GetLocalizedString("Retry");

        _isProgressCritical = true;
        OnPropertyChanged(nameof(IsProgressCritical));
        Disconnect();
    }

    private void StoreHelper_ItemActionsStopped(string obj)
    {
        if (obj == StoreId)
            Disconnect();
    }

    private void StoreHelper_ItemDownloadSpeedChanged(Tuple<string, double> obj)
    {
        string operationId = obj.Item1;
        double speed = obj.Item2;

        if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
            return;

        SpeedText = $"{UnitsParser.FormatSpeed(speed)}, ";
    }

    private void StoreHelper_ItemDownloadProgressChanged(Tuple<string, double> obj)
    {
        string operationId = obj.Item1;
        double progress = obj.Item2;

        if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
            return;

        IsProgressIndeterminate = false;
        ProgressValue = progress;
    }

    private void StoreHelper_ItemTimeRemainingChanged(Tuple<string, TimeSpan> obj)
    {
        string operationId = obj.Item1;
        TimeSpan time = obj.Item2;

        if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
            return;

        TimeText = UnitsParser.ConvertMinutesToPrettyText(time.TotalMinutes);
    }

    private void StoreHelper_ItemDownloadStageChanged(Tuple<string, string> obj)
    {
        string operationId = obj.Item1;
        string stage = obj.Item2;

        if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
            return;

        ApplyStage(stage);
    }

    private void ApplyStage(string state)
    {
        IsProgressVisible = true;
        string stageHeaderText;
        switch (state)
        {
            case "GETR":
                stageHeaderText = _localizer.GetLocalizedString("GettingReady");
                IsProgressIndeterminate = true;
                break;
            case "END":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                IsProgressIndeterminate = true;
                break;
            case "Downloading":
                stageHeaderText = _localizer.GetLocalizedString("Downloading");
                IsProgressIndeterminate = false;
                break;
            case "Extracting":
                stageHeaderText = _localizer.GetLocalizedString("Installing");
                IsProgressIndeterminate = true;
                break;
            case "ErrorHappens":
                stageHeaderText = _localizer.GetLocalizedString("ErrorHappens");
                break;
            case "Completed":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                IsProgressIndeterminate = true;
                break;
            case "CANC":
                stageHeaderText = _localizer.GetLocalizedString("Cancel");
                IsProgressIndeterminate = true;
                break;
            case "ConnectingToService":
                stageHeaderText = _localizer.GetLocalizedString("ConnectingToService");
                IsProgressIndeterminate = true;
                break;
            case "":
                stageHeaderText = _localizer.GetLocalizedString("QueueWaiting");
                IsProgressIndeterminate = false;
                ProgressValue = 0;
                break;
            default:
                stageHeaderText = _localizer.GetLocalizedString(state);
                IsProgressIndeterminate = true;
                break;
        }

        StatusText = stageHeaderText;
    }
}
