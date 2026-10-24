using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.PrettyErrorConversionService;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Store;

/// <summary>
/// State of the small window that installs one Store item: the item card, the queue
/// progress of the item and its result. The Store events are handled on the thread that
/// raises them, as the window did before.
/// </summary>
public sealed partial class StoreSmallDownloadDialogViewModel : ObservableObject
{
    private const string LogSource = "StoreSmallDownloadDialog";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly Func<List<ItemLicenseModel>, Task<bool>> _acceptLicensesAsync;

    private string _storeId = string.Empty;
    private RepoItemModel _item;
    private bool _errorHappened;

    private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
    private Action<Tuple<string, TimeSpan>> _itemTimeRemainingChangedHandler;
    private Action<Tuple<string, ErrorModel>> _itemInstallingErrorHappensHandler;
    private Action<string> _itemActionsStoppedHandler;

    private bool _isLoading = true;
    private string _itemName = string.Empty;
    private ImageSource _itemIcon;
    private string _developerText = string.Empty;
    private string _categoryText = string.Empty;
    private string _smallDescription = string.Empty;
    private bool _isDescriptionVisible = true;
    private bool _isErrorVisible;
    private string _errorCode = string.Empty;
    private bool _isDownloadProgressVisible;
    private string _statusText = "Getting ready...";
    private bool _isSpeedVisible = true;
    private string _speedText = string.Empty;
    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private bool _isGetVisible = true;
    private string _cancelButtonText;

    /// <param name="acceptLicensesAsync">Shows the licenses; returns <see langword="true"/> when the user accepts them.</param>
    public StoreSmallDownloadDialogViewModel(Func<List<ItemLicenseModel>, Task<bool>> acceptLicensesAsync)
    {
        _acceptLicensesAsync = acceptLicensesAsync;
    }

    public string StoreId => _storeId;

    /// <summary>
    /// <see langword="true"/> while the item is loaded: the placeholders are shown and the
    /// buttons are disabled.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(IsLoaded));
        }
    }

    public bool IsLoaded => !_isLoading;

    public string ItemName
    {
        get => _itemName;
        private set => SetProperty(ref _itemName, value);
    }

    public ImageSource ItemIcon
    {
        get => _itemIcon;
        private set => SetProperty(ref _itemIcon, value);
    }

    public string DeveloperText
    {
        get => _developerText;
        private set => SetProperty(ref _developerText, value);
    }

    public string CategoryText
    {
        get => _categoryText;
        private set => SetProperty(ref _categoryText, value);
    }

    public string SmallDescription
    {
        get => _smallDescription;
        private set => SetProperty(ref _smallDescription, value);
    }

    public bool IsDescriptionVisible
    {
        get => _isDescriptionVisible;
        private set => SetProperty(ref _isDescriptionVisible, value);
    }

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set => SetProperty(ref _isErrorVisible, value);
    }

    public string ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    public bool IsDownloadProgressVisible
    {
        get => _isDownloadProgressVisible;
        private set => SetProperty(ref _isDownloadProgressVisible, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsSpeedVisible
    {
        get => _isSpeedVisible;
        private set => SetProperty(ref _isSpeedVisible, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
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

    public bool IsGetVisible
    {
        get => _isGetVisible;
        private set => SetProperty(ref _isGetVisible, value);
    }

    /// <summary>
    /// The text of the cancel button once it changes. The button also has a localization uid,
    /// so the view copies this value to it each time it is set, as the window did before.
    /// </summary>
    public string CancelButtonText
    {
        get => _cancelButtonText;
        private set
        {
            _cancelButtonText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Loads the Store database and shows the item.</summary>
    public async Task SetItemToViewIdAsync(string id)
    {
        try
        {
            _storeId = id;
            await StoreHelper.Instance.LoadAllStoreDatabase(forseSync: true);
            LoadItemInfo();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    /// <summary>Removes the item from the queue; called before the window closes.</summary>
    public void Cancel()
    {
        StoreHelper.Instance.RemoveItemFromQueue(_storeId);
    }

    /// <summary>Asks for the licenses and queues the item.</summary>
    public async Task StartDownloadAsync()
    {
        try
        {
            if (!await AskLicenseAsync())
                return;

            ShowDownloadState();

            StatusText = _localizer.GetLocalizedString("QueueWaiting");
            StoreHelper.Instance.AddItemToQueue(_storeId, string.Empty);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    /// <summary>Stops the queue handlers; called when the window is closed.</summary>
    public void Detach()
    {
        if (_itemDownloadStageChangedHandler != null)
            StoreHelper.Instance.ItemDownloadStageChanged -= _itemDownloadStageChangedHandler;

        if (_itemDownloadProgressChangedHandler != null)
            StoreHelper.Instance.ItemDownloadProgressChanged -= _itemDownloadProgressChangedHandler;

        if (_itemTimeRemainingChangedHandler != null)
            StoreHelper.Instance.ItemTimeRemainingChanged -= _itemTimeRemainingChangedHandler;

        if (_itemInstallingErrorHappensHandler != null)
            StoreHelper.Instance.ItemInstallingErrorHappens -= _itemInstallingErrorHappensHandler;

        if (_itemDownloadSpeedChangedHandler != null)
            StoreHelper.Instance.ItemDownloadSpeedChanged -= _itemDownloadSpeedChangedHandler;

        if (_itemActionsStoppedHandler != null)
            StoreHelper.Instance.ItemActionsStopped -= _itemActionsStoppedHandler;
    }

    private void LoadItemInfo()
    {
        _item = StoreHelper.Instance.GetItemInfoFromStoreId(_storeId);

        if (_item == null)
            return;

        ConnectHandlers();
        CheckCurrentStatus();

        ItemName = string.Format(_localizer.GetLocalizedString("StoreSmallItemName"), _item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(_item.name, StoreLocalizationHelper.GetStoreLikeLocale()));
        ItemIcon = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(_item.icon)));
        DeveloperText = string.Format(_localizer.GetLocalizedString("StoreSmallDeveloperText"), _item.developer);
        CategoryText = string.Format(_localizer.GetLocalizedString("StoreSmallCategoryText"), StoreHelper.Instance.GetLocalizedStoreItemName(
                StoreHelper.Instance.GetCategoryFromStoreId(_item.category_id).name,
                StoreLocalizationHelper.GetStoreLikeLocale()
            ));
        SmallDescription = LScriptLangHelper.ExecuteScript(_item.small_description, StoreLocalizationHelper.GetStoreLikeLocale());

        IsLoading = false;

        if (DatabaseHelper.Instance.IsItemInstalled(_storeId))
        {
            CancelButtonText = _localizer.GetLocalizedString("OkButtonText");

            IsDescriptionVisible = false;
            IsDownloadProgressVisible = false;
            IsErrorVisible = true;
            IsGetVisible = false;

            ErrorCode = "ERR_ITEM_ALLREADY_INSTALLED";
        }
    }

    private async Task<bool> AskLicenseAsync()
    {
        if (_item.license == null || _item.license.Count == 0)
            return true;

        return await _acceptLicensesAsync(_item.license);
    }

    private void ShowDownloadState()
    {
        IsGetVisible = false;
        IsProgressIndeterminate = true;

        IsErrorVisible = false;
        IsDownloadProgressVisible = true;
    }

    private void CheckCurrentStatus()
    {
        string operationId = StoreHelper.Instance.GetOperationIdFromItemId(_storeId);
        if (!string.IsNullOrEmpty(operationId))
        {
            ShowDownloadState();

            string status = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadStage;
            ApplyStage(status);
        }
    }

    private void ApplyStage(string state)
    {
        string stageHeaderText = string.Empty;
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
                IsSpeedVisible = true;
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
            default:
                stageHeaderText = _localizer.GetLocalizedString(state);
                IsProgressIndeterminate = true;
                break;
        }
        StatusText = stageHeaderText;
    }

    private void ConnectHandlers()
    {
        _itemDownloadStageChangedHandler = (data) =>
        {
            string operationId = data.Item1;
            string stage = data.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                return;

            ShowDownloadState();

            IsSpeedVisible = false;

            ApplyStage(stage);
        };
        StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

        _itemDownloadProgressChangedHandler = (data) =>
        {
            string operationId = data.Item1;
            double progress = data.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                return;

            IsProgressIndeterminate = false;
            ProgressValue = progress;
        };
        StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

        _itemTimeRemainingChangedHandler = (data) =>
        {
            string operationId = data.Item1;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                return;
        };

        StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

        _itemDownloadSpeedChangedHandler = (data) =>
        {
            string operationId = data.Item1;
            double speed = data.Item2;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                return;

            SpeedText = $"{UnitsParser.FormatSpeed(speed)}";
        };

        StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

        _itemInstallingErrorHappensHandler = (data) =>
        {
            string operationId = data.Item1;
            string errorCode = data.Item2.ErrorCode;

            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                return;

            IsDescriptionVisible = false;
            IsDownloadProgressVisible = false;
            IsErrorVisible = true;

            ErrorCode = errorCode;
            _errorHappened = true;

            CancelButtonText = _localizer.GetLocalizedString("OkButtonText");
        };
        StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

        // The event fires for every item; "installed" was shown after an error or when
        // another item finished.
        _itemActionsStoppedHandler = (id) =>
        {
            if (id != _storeId || _errorHappened)
                return;
            ShowItemAfterInstallActions();
        };
        StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
    }

    private void ShowItemAfterInstallActions()
    {
        if (_item == null)
            return;
        IsDownloadProgressVisible = false;
        CancelButtonText = _localizer.GetLocalizedString("OkButtonText");

        ItemName = string.Format(_localizer.GetLocalizedString("StoreSmallItemNameComplete"), _item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(_item.name, StoreLocalizationHelper.GetStoreLikeLocale()));
    }
}
