using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Helper.LScript;
using CDPIUI.Shared;
using CDPIUI.Views.Store;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Store;

/// <summary>
/// State of the Store item page: the item card, the install, setup and delete actions, and
/// the progress of the item in the download queue.
/// </summary>
public sealed partial class ItemViewPageViewModel : ObservableObject
{
    private const string LogSource = "ItemViewPage";

    private readonly IItemViewPageView _view;
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly DispatcherQueue _dispatcherQueue;

    private string _storeId;
    private RepoItemModel _item;
    private bool _errorHappens;
    private bool _loaded;

    private string _itemName = "Item Name";
    private ImageSource _itemIcon = new BitmapImage(new Uri("ms-appx:///Assets/Store/empty.png"));
    private string _developer = "Item Developer";
    private string _stars = "NaN";
    private string _categoryName = "Item Category";
    private string _smallDescription = "Item Small Description Text";
    private string _fullDescription = "Item Full Description Text";
    private bool _isWarningVisible = true;
    private string _warningText = "";
    private bool _isActionEnabled = true;
    private bool _isActionVisible = true;
    private string _actionText = "Item Action Button Text";
    private bool _isMoreVisible;
    private bool _isUnsupportedWarningVisible;
    private bool _isLinksVisible = true;
    private bool _isDownloadStatusVisible;
    private bool _isErrorStatusVisible;
    private bool _isStopEnabled = true;
    private string _statusText = string.Empty;
    private bool _isSpeedVisible;
    private string _speedText = "";
    private string _statusTip;
    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private string _errorName = "ERR_FLASHLIGHT_BISY";

    public ItemViewPageViewModel(IItemViewPageView view, DispatcherQueue dispatcherQueue)
    {
        _view = view;
        _dispatcherQueue = dispatcherQueue;
        DeleteCommand = new RelayCommand(Delete);
        ShowFolderCommand = new RelayCommand(ShowFolder);
        LaunchUnsupportedHelpCommand = new RelayCommand(LaunchUnsupportedHelp);
    }

    public RelayCommand DeleteCommand { get; }
    public RelayCommand ShowFolderCommand { get; }
    public RelayCommand LaunchUnsupportedHelpCommand { get; }

    public ObservableCollection<AdditionalItemInfoModel> AdditionalInfo { get; } = [];

    /// <summary><see langword="true"/> when the item was found and shown.</summary>
    public bool IsLoaded => _loaded;

    public string StoreId => _storeId;

    public string CategoryId => _item.category_id;

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

    public string Developer
    {
        get => _developer;
        private set => SetProperty(ref _developer, value);
    }

    public string Stars
    {
        get => _stars;
        private set => SetProperty(ref _stars, value);
    }

    public string CategoryName
    {
        get => _categoryName;
        private set => SetProperty(ref _categoryName, value);
    }

    public string SmallDescription
    {
        get => _smallDescription;
        private set => SetProperty(ref _smallDescription, value);
    }

    public string FullDescription
    {
        get => _fullDescription;
        private set => SetProperty(ref _fullDescription, value);
    }

    public bool IsWarningVisible
    {
        get => _isWarningVisible;
        private set => SetProperty(ref _isWarningVisible, value);
    }

    public string WarningText
    {
        get => _warningText;
        private set => SetProperty(ref _warningText, value);
    }

    public bool IsActionEnabled
    {
        get => _isActionEnabled;
        private set => SetProperty(ref _isActionEnabled, value);
    }

    public bool IsActionVisible
    {
        get => _isActionVisible;
        private set => SetProperty(ref _isActionVisible, value);
    }

    public string ActionText
    {
        get => _actionText;
        private set => SetProperty(ref _actionText, value);
    }

    public bool IsMoreVisible
    {
        get => _isMoreVisible;
        private set => SetProperty(ref _isMoreVisible, value);
    }

    public bool IsUnsupportedWarningVisible
    {
        get => _isUnsupportedWarningVisible;
        private set => SetProperty(ref _isUnsupportedWarningVisible, value);
    }

    public bool IsLinksVisible
    {
        get => _isLinksVisible;
        private set => SetProperty(ref _isLinksVisible, value);
    }

    public bool IsDownloadStatusVisible
    {
        get => _isDownloadStatusVisible;
        private set => SetProperty(ref _isDownloadStatusVisible, value);
    }

    public bool IsErrorStatusVisible
    {
        get => _isErrorStatusVisible;
        private set => SetProperty(ref _isErrorStatusVisible, value);
    }

    public bool IsStopEnabled
    {
        get => _isStopEnabled;
        private set => SetProperty(ref _isStopEnabled, value);
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

    /// <summary>
    /// The tip under the status. The text block also has a localization uid, so the view
    /// copies this value to it each time it is set, as the page did before.
    /// </summary>
    public string StatusTip
    {
        get => _statusTip;
        private set
        {
            _statusTip = value;
            OnPropertyChanged();
        }
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

    public string ErrorName
    {
        get => _errorName;
        private set => SetProperty(ref _errorName, value);
    }

    /// <summary>Shows the item; called when the page is opened with an item id.</summary>
    public void Load(string storeId)
    {
        _storeId = storeId;

        ConnectHandlers();

        _item = StoreHelper.Instance.GetItemInfoFromStoreId(_storeId);

        if (_item == null || _storeId == SharedConstants.ApplicationStoreId)
        {
            return;
        }

        ItemName = _item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(_item.name, StoreLocalizationHelper.GetStoreLikeLocale());
        ItemIcon = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(_item.icon)));
        Developer = _item.developer;
        Logger.Instance.CreateDebugLog(LogSource, _item.category_id);
        Stars = _item.stars ?? "NaN";
        CategoryName = StoreHelper.Instance.GetLocalizedStoreItemName(
            StoreHelper.Instance.GetCategoryFromStoreId(_item.category_id)?.name ?? string.Empty,
            StoreLocalizationHelper.GetStoreLikeLocale()
        );
        SmallDescription = LScriptLangHelper.ExecuteScript(_item.small_description, StoreLocalizationHelper.GetStoreLikeLocale());
        FullDescription = LScriptLangHelper.ExecuteScript(_item.description, StoreLocalizationHelper.GetStoreLikeLocale());
        IsWarningVisible = _item.display_warning;
        WarningText = LScriptLangHelper.ExecuteScript(_item.warning_text, StoreLocalizationHelper.GetStoreLikeLocale());

        if (DatabaseHelper.Instance.IsItemInstalled(_storeId))
        {
            IsActionEnabled = _item.type == "component";
            ActionText = _localizer.GetLocalizedString("Setup");
            IsMoreVisible = true;
        }
        else
        {
            ActionText = _localizer.GetLocalizedString("Install");
            IsMoreVisible = false;

            bool isItemSupported = IsItemSupported();

            IsActionEnabled = isItemSupported;
            IsUnsupportedWarningVisible = !isItemSupported;
        }

        if (_item.links?.Count > 0)
            _view.ShowLinks(_item.links);
        else
            IsLinksVisible = false;

        _loaded = true;

        LoadAdvancedItemInfo();
        CheckCurrentStatus();
    }

    /// <summary>Called when the page is opened without an item id.</summary>
    public void ShowTemplate()
    {
        ItemName = "Template page";
    }

    /// <summary>Stops the queue handlers; called when the page is left.</summary>
    public void Detach()
    {
        DisconnectHandlers();
    }

    /// <summary>Installs the item, or opens the settings of the installed component.</summary>
    public async Task RunItemActionAsync()
    {
        if (!DatabaseHelper.Instance.IsItemInstalled(_storeId))
        {
            await InstallAsync();
        }
        else
        {
            await _view.OpenComponentSettingsAsync(_storeId);
        }
    }

    public void StopAction()
    {
        StoreHelper.Instance.RemoveItemFromQueue(_storeId);
    }

    public async Task ReinstallAsync()
    {
        // TODO: downloading from saved URL

        if (await _view.ConfirmReinstallAsync())
        {
            // The old files must be gone before the new ones are written.
            await StoreHelper.Instance.RemoveItemAsync(_storeId);
            await InstallAsync();
        }
    }

    private void Delete()
    {
        IsStopEnabled = false;
        StatusText = _localizer.GetLocalizedString("QueueWaiting");
        IsProgressIndeterminate = true;
        IsErrorStatusVisible = false;
        IsActionVisible = false;
        IsDownloadStatusVisible = true;

        StoreHelper.Instance.RemoveItem(_storeId);

        IsActionEnabled = IsItemSupported();
    }

    private void ShowFolder()
    {
        try
        {
            ShellHelper.OpenFileInDefaultApp(DatabaseHelper.Instance.GetItemById(_item.store_id)?.Directory ?? string.Empty);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void LaunchUnsupportedHelp()
    {
        CommandsHandler.HandleCommand(
            "cdpiui://Help/Store/ItemUnsupportedWarning/");
    }

    private void LoadAdvancedItemInfo()
    {
        _item = StoreHelper.Instance.GetItemInfoFromStoreId(_storeId);
        AdditionalInfo.Clear();
        if (_item == null)
        {
            return;
        }
        foreach (AdditionalItemInfoModel info in AdditionalItemInfoBuilder.Build(_item))
            AdditionalInfo.Add(info);
    }

    private bool IsItemSupported()
    {
        return VersionHelper.IsVersionInRange(ApplicationInfo.Version, _item.target_minversion, _item.target_maxversion);
    }

    private async Task InstallAsync()
    {
        if (!await AskLicenseAsync())
            return;
        ConnectHandlers();
        _errorHappens = false;

        ShowDownloadState();

        StatusText = _localizer.GetLocalizedString("QueueWaiting");
        StoreHelper.Instance.AddItemToQueue(_storeId, string.Empty);

        IsActionEnabled = _item.type == "component";
    }

    private async Task<bool> AskLicenseAsync()
    {
        if (_item.license == null || _item.license.Count == 0)
            return true;

        return await _view.AcceptLicensesAsync(_item.license);
    }

    private void ShowDownloadState()
    {
        IsStopEnabled = true;
        IsProgressIndeterminate = true;
        IsErrorStatusVisible = false;
        IsActionVisible = false;
        IsDownloadStatusVisible = true;
    }

    private void CheckCurrentStatus()
    {
        string operationId = StoreHelper.Instance.GetOperationIdFromItemId(_storeId);
        if (!string.IsNullOrEmpty(operationId))
        {
            ShowDownloadState();

            string status = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadStage;
            ApplyStage(status ?? "QueueWaiting");
            ProgressValue = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadProgress ?? 0;
        }
    }

    private void ApplyStage(string state)
    {
        state ??= "QueueWaiting";
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
                StatusTip = _localizer.GetLocalizedString("ConnectingToServiceTip");
                IsProgressIndeterminate = true;
                break;
            default:
                stageHeaderText = _localizer.GetLocalizedString(state);
                IsProgressIndeterminate = true;
                break;
        }
        StatusText = stageHeaderText;
    }

    private void OnStageChanged(string stage)
    {
        if (_errorHappens)
            return;

        ShowDownloadState();

        IsSpeedVisible = false;

        ApplyStage(stage);
    }

    private void OnProgressChanged(double progress)
    {
        if (_errorHappens)
            return;

        IsProgressIndeterminate = false;
        ProgressValue = progress;
    }

    private void OnTimeRemainingChanged(TimeSpan time)
    {
        if (_errorHappens)
            return;

        if (time.TotalMinutes >= 1)
            StatusTip = string.Format(_localizer.GetLocalizedString("/UIHelper/LeftMinutes"), (int)time.TotalMinutes);
        else
            StatusTip = _localizer.GetLocalizedString("/UIHelper/LeftSmall");
    }

    private void OnSpeedChanged(string speedText)
    {
        if (_errorHappens)
            return;

        SpeedText = speedText;
    }

    private void OnInstallingError(string errorCode)
    {
        IsErrorStatusVisible = true;
        IsActionVisible = false;
        IsDownloadStatusVisible = false;

        ErrorName = errorCode;
        _errorHappens = true;
    }

    private void ShowItemAfterInstallActions(string id)
    {
        if (id != _storeId)
            return;
        ProgressValue = 0;
        IsProgressIndeterminate = true;

        IsSpeedVisible = false;
        SpeedText = "";
        StatusTip = _localizer.GetLocalizedString("WorkingOnIt");

        if (id == _storeId && !_errorHappens)
        {
            IsActionEnabled = IsItemSupported();
            ActionText = _localizer.GetLocalizedString("Install");
            IsMoreVisible = false;
            IsActionVisible = true;
            IsDownloadStatusVisible = false;

            if (DatabaseHelper.Instance.IsItemInstalled(id))
            {
                ActionText = _localizer.GetLocalizedString("Setup");
                IsMoreVisible = true;

                IsActionEnabled = _item.type == "component";
            }
        }
    }
}
