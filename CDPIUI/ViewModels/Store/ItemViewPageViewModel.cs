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
    private readonly ItemDownloadPresentation _downloadPresentation;

    private string _storeId;
    private RepoItemModel _item;
    private bool _loaded;

    private string _itemName = string.Empty;
    private ImageSource _itemIcon = new BitmapImage(new Uri("ms-appx:///Assets/Store/empty.png"));
    private string _developer = string.Empty;
    private string _stars = "NaN";
    private string _categoryName = string.Empty;
    private string _smallDescription = string.Empty;
    private string _fullDescription = string.Empty;
    private bool _isWarningVisible = true;
    private string _warningText = "";
    private bool _isActionEnabled = true;
    private string _actionText = string.Empty;
    private bool _isMoreVisible;
    private bool _isUnsupportedWarningVisible;
    private bool _isLinksVisible = true;

    public ItemViewPageViewModel(IItemViewPageView view, DispatcherQueue dispatcherQueue)
    {
        _view = view;
        _dispatcherQueue = dispatcherQueue;
        _downloadPresentation = new ItemDownloadPresentation(key => _localizer.GetLocalizedString(key));
        _downloadPresentation.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
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
        get => _downloadPresentation.IsActionVisible;
        private set => _downloadPresentation.IsActionVisible = value;
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
        get => _downloadPresentation.IsDownloadStatusVisible;
        private set => _downloadPresentation.IsDownloadStatusVisible = value;
    }

    public bool IsErrorStatusVisible
    {
        get => _downloadPresentation.IsErrorStatusVisible;
        private set => _downloadPresentation.IsErrorStatusVisible = value;
    }

    public bool IsStopEnabled
    {
        get => _downloadPresentation.IsStopEnabled;
        private set => _downloadPresentation.IsStopEnabled = value;
    }

    public string StatusText
    {
        get => _downloadPresentation.StatusText;
        private set => _downloadPresentation.StatusText = value;
    }

    public bool IsSpeedVisible
    {
        get => _downloadPresentation.IsSpeedVisible;
        private set => _downloadPresentation.IsSpeedVisible = value;
    }

    public string SpeedText
    {
        get => _downloadPresentation.SpeedText;
        private set => _downloadPresentation.SpeedText = value;
    }

    /// <summary>
    /// The tip under the status. The text block also has a localization uid, so the view
    /// copies this value to it each time it is set, as the page did before.
    /// </summary>
    public string StatusTip
    {
        get => _downloadPresentation.StatusTip;
        private set => _downloadPresentation.StatusTip = value;
    }

    public bool IsProgressIndeterminate
    {
        get => _downloadPresentation.IsProgressIndeterminate;
        private set => _downloadPresentation.IsProgressIndeterminate = value;
    }

    public double ProgressValue
    {
        get => _downloadPresentation.ProgressValue;
        private set => _downloadPresentation.ProgressValue = value;
    }

    public string ErrorName
    {
        get => _downloadPresentation.ErrorName;
        private set => _downloadPresentation.ErrorName = value;
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
        ItemName = _localizer.GetLocalizedString("StoreTemplatePage");
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
        _downloadPresentation.ErrorHappens = false;

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

    private void ShowDownloadState() =>
        _downloadPresentation.ShowDownloadState();

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

    private void ApplyStage(string state) =>
        _downloadPresentation.ApplyStage(state);

    private void OnStageChanged(string stage) =>
        _downloadPresentation.OnStageChanged(stage);

    private void OnProgressChanged(double progress) =>
        _downloadPresentation.OnProgressChanged(progress);

    private void OnTimeRemainingChanged(TimeSpan time) =>
        _downloadPresentation.OnTimeRemainingChanged(time);

    private void OnSpeedChanged(string speedText) =>
        _downloadPresentation.OnSpeedChanged(speedText);

    private void OnInstallingError(string errorCode) =>
        _downloadPresentation.OnInstallingError(errorCode);

    private void ShowItemAfterInstallActions(string id)
    {
        if (id != _storeId)
            return;
        ProgressValue = 0;
        IsProgressIndeterminate = true;

        IsSpeedVisible = false;
        SpeedText = "";
        StatusTip = _localizer.GetLocalizedString("WorkingOnIt");

        if (id == _storeId && !_downloadPresentation.ErrorHappens)
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
