using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Core;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using CDPIUI.Helper.LScript;
using CDPIUI.Views.Store;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Store;

/// <summary>
/// State of the ready kit page: the kit card, its items, and the progress of installing
/// all items of the kit through the download queue.
/// </summary>
public sealed partial class ReadyKitViewPageViewModel : ObservableObject
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly Dictionary<string, double> _itemProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<List<ItemLicenseModel>, Task<bool>> _acceptLicensesAsync;
    private readonly DispatcherQueue _dispatcherQueue;

    private ReadyKitModel _kit;
    private List<RepoItemModel> _items = [];
    private bool _canInstall;
    private bool _hasInstallationError;
    private bool _isPageActive;
    private bool _loaded;
    private string _activeItemId = string.Empty;
    private string _loadErrorTitle = string.Empty;
    private string _loadErrorMessage = string.Empty;
    private string _loadErrorCode = string.Empty;

    private string _kitName = string.Empty;
    private ImageSource _kitIcon = new BitmapImage(new Uri("ms-appx:///Assets/Store/empty.png"));
    private Brush _kitIconBackground;
    private string _smallDescription = string.Empty;
    private string _description = string.Empty;
    private string _contentsTitle = string.Empty;
    private string _readyKitsCategoryText = string.Empty;
    private string _itemsCountText = string.Empty;
    private string _installText = string.Empty;
    private bool _isInstallEnabled = true;
    private bool _isInstallVisible = true;
    private bool _isDownloadStatusVisible;
    private bool _isErrorStatusVisible;
    private bool _isStopEnabled = true;
    private string _statusText = string.Empty;
    private bool _isSpeedVisible;
    private string _speedText = string.Empty;
    private string _statusTip = string.Empty;
    private double _progressValue;
    private string _errorName = string.Empty;
    private string _unsupportedWarningText = string.Empty;
    private bool _isUnsupportedWarningVisible;

    /// <param name="acceptLicensesAsync">Shows the licenses; returns <see langword="true"/> when the user accepts them.</param>
    public ReadyKitViewPageViewModel(Func<List<ItemLicenseModel>, Task<bool>> acceptLicensesAsync, DispatcherQueue dispatcherQueue)
    {
        _acceptLicensesAsync = acceptLicensesAsync;
        _dispatcherQueue = dispatcherQueue;
        LaunchKitUnsupportedHelpCommand = new RelayCommand(LaunchKitUnsupportedHelp);
    }

    public RelayCommand LaunchKitUnsupportedHelpCommand { get; }

    public ObservableCollection<ReadyKitContentItemViewModel> ContentItems { get; } = [];

    /// <summary><see langword="true"/> when the kit was found and shown.</summary>
    public bool IsLoaded => _loaded;

    public string LoadErrorTitle => _loadErrorTitle;

    public string LoadErrorMessage => _loadErrorMessage;

    public string LoadErrorCode => _loadErrorCode;

    public string KitName
    {
        get => _kitName;
        private set => SetProperty(ref _kitName, value);
    }

    public ImageSource KitIcon
    {
        get => _kitIcon;
        private set => SetProperty(ref _kitIcon, value);
    }

    public Brush KitIconBackground
    {
        get => _kitIconBackground;
        private set => SetProperty(ref _kitIconBackground, value);
    }

    public string SmallDescription
    {
        get => _smallDescription;
        private set => SetProperty(ref _smallDescription, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string ContentsTitle
    {
        get => _contentsTitle;
        private set => SetProperty(ref _contentsTitle, value);
    }

    public string ReadyKitsCategoryText
    {
        get => _readyKitsCategoryText;
        private set => SetProperty(ref _readyKitsCategoryText, value);
    }

    public string ItemsCountText
    {
        get => _itemsCountText;
        private set => SetProperty(ref _itemsCountText, value);
    }

    public string InstallText
    {
        get => _installText;
        private set => SetProperty(ref _installText, value);
    }

    public bool IsInstallEnabled
    {
        get => _isInstallEnabled;
        private set => SetProperty(ref _isInstallEnabled, value);
    }

    public bool IsInstallVisible
    {
        get => _isInstallVisible;
        private set => SetProperty(ref _isInstallVisible, value);
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

    public string StatusTip
    {
        get => _statusTip;
        private set => SetProperty(ref _statusTip, value);
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

    public string UnsupportedWarningText
    {
        get => _unsupportedWarningText;
        private set => SetProperty(ref _unsupportedWarningText, value);
    }

    public bool IsUnsupportedWarningVisible
    {
        get => _isUnsupportedWarningVisible;
        private set => SetProperty(ref _isUnsupportedWarningVisible, value);
    }

    /// <summary>Shows the kit; called when the page is opened.</summary>
    public void Load(string kitId)
    {
        _isPageActive = true;

        _kit = StoreHelper.Instance.GetReadyKitFromStoreId(kitId);
        if (_kit == null)
        {
            SetLoadError(
                _localizer.GetLocalizedString("ReadyKitInvalidTitle"),
                _localizer.GetLocalizedString("ReadyKitInvalidMessage"),
                "ERR_STORE_READY_KIT_NOT_FOUND");
            return;
        }

        if (!LoadKit())
            return;

        _loaded = true;
        ConnectQueueHandlers();
        RefreshActionState();
    }

    /// <summary>Stops the queue handlers and releases the images; called when the page is left.</summary>
    public void Detach()
    {
        _isPageActive = false;
        DisconnectQueueHandlers();

        foreach (ReadyKitContentItemViewModel item in ContentItems)
            item.ImageSource = null;

        KitIcon = null;
    }

    private bool LoadKit()
    {
        _items = (_kit.items ?? [])
            .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
            .OfType<RepoItemModel>()
            .ToList();

        string locale = StoreLocalizationHelper.GetStoreLikeLocale();
        KitName = _kit.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(
            _kit.name,
            locale);
        SmallDescription = LScriptLangHelper.ExecuteScript(_kit.small_description, locale);
        Description = LScriptLangHelper.ExecuteScript(_kit.description, locale);
        KitIcon = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(_kit.icon)));
        KitIconBackground = ReadyKitBrushFactory.Create(
            _items.Select(item => item.background),
            _kit.background);
        ContentsTitle = _localizer.GetLocalizedString("ReadyKitContents");
        ReadyKitsCategoryText = _localizer.GetLocalizedString("ReadyKitsPageTitle");
        ItemsCountText = _items.Count.ToString();

        ContentItems.Clear();
        foreach (RepoItemModel item in _items)
        {
            ContentItems.Add(new ReadyKitContentItemViewModel
            {
                StoreId = item.store_id,
                Name = GetItemDisplayName(item),
                Developer = item.developer,
                Category = StoreHelper.Instance.GetLocalizedStoreItemName(
                           StoreHelper.Instance.GetCategoryFromStoreId(item.category_id)?.name ?? string.Empty,
                           StoreLocalizationHelper.GetStoreLikeLocale()),
                Description = LScriptLangHelper.ExecuteScript(item.small_description, StoreLocalizationHelper.GetStoreLikeLocale()),
                ImageSource = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(item.icon))),
                BackgroundBrush = UIHelper.HexToSolidColorBrushConverter(item.background)
            });
        }

        int missingItemCount = (_kit.items ?? [])
            .Count(itemId => StoreHelper.Instance.GetItemInfoFromStoreId(itemId) == null);
        if (_items.Count == 0)
        {
            SetLoadError(
                _localizer.GetLocalizedString("ReadyKitInvalidTitle"),
                _localizer.GetLocalizedString("ReadyKitInvalidMessage"),
                "ERR_STORE_READY_KIT_EMPTY");
            return false;
        }

        if (missingItemCount > 0)
        {
            SetLoadError(
                _localizer.GetLocalizedString("ReadyKitInvalidTitle"),
                string.Format(
                    _localizer.GetLocalizedString("ReadyKitMissingItemsMessage"),
                    missingItemCount),
                "ERR_STORE_READY_KIT_ITEM_NOT_FOUND");
            return false;
        }

        if (_items.Any(item => !IsItemSupported(item)))
        {
            DisableInstallation(_localizer.GetLocalizedString("ReadyKitUnsupportedMessage"));
            return true;
        }

        _canInstall = true;
        return true;
    }

    /// <summary>Queues the items of the kit that are not installed yet.</summary>
    public async Task InstallAsync()
    {
        if (!_canInstall)
            return;

        List<RepoItemModel> itemsToInstall = _items
            .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id))
            .ToList();
        if (itemsToInstall.Count == 0)
            return;

        List<ItemLicenseModel> licenses = StoreItemLicenses.CollectDistinct(itemsToInstall);

        if (licenses.Count > 0)
        {
            if (!await _acceptLicensesAsync(licenses))
                return;
        }

        _hasInstallationError = false;
        IsErrorStatusVisible = false;
        foreach (RepoItemModel item in itemsToInstall)
            _itemProgress[item.store_id] = 0;

        ShowDownloadState();
        StatusText = _localizer.GetLocalizedString("QueueWaiting");
        IsSpeedVisible = false;

        HashSet<string> pendingIds = itemsToInstall
            .Select(item => item.store_id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string itemId in _kit.items ?? [])
        {
            if (pendingIds.Contains(itemId))
                StoreHelper.Instance.AddItemToQueue(itemId, string.Empty);
        }

        RefreshActionState();
    }

    /// <summary>Removes the items of the kit from the queue.</summary>
    public void Stop()
    {
        IsStopEnabled = false;
        StatusText = _localizer.GetLocalizedString("Cancel");
        _hasInstallationError = false;

        foreach (RepoItemModel item in _items)
            StoreHelper.Instance.RemoveItemFromQueue(item.store_id);
    }

    private void LaunchKitUnsupportedHelp()
    {
        CommandsHandler.HandleCommand("cdpiui://Help/Store/ItemUnsupportedWarning/");
    }

    private void DisableInstallation(string message)
    {
        _canInstall = false;
        InstallText = _localizer.GetLocalizedString("Get");
        IsInstallEnabled = false;
        UnsupportedWarningText = message;
        IsUnsupportedWarningVisible = true;
    }

    private void SetLoadError(string title, string message, string errorCode)
    {
        _canInstall = false;
        _loadErrorTitle = title;
        _loadErrorMessage = message;
        _loadErrorCode = errorCode;
    }

    private string GetItemDisplayName(RepoItemModel item) =>
        item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(
            item.name,
            StoreLocalizationHelper.GetStoreLikeLocale());

    private static bool IsItemSupported(RepoItemModel item)
    {
        if (!Version.TryParse(ApplicationInfo.Version, out Version currentVersion) ||
            !Version.TryParse(item.target_minversion, out Version minVersion))
        {
            return false;
        }

        if (currentVersion < minVersion)
            return false;

        return item.target_maxversion == "NaN" ||
            (Version.TryParse(item.target_maxversion, out Version maxVersion) && currentVersion <= maxVersion);
    }
}
