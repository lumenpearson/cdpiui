using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using CDPIUI.Commands;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.PrettyErrorConversionService;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;

namespace CDPIUI.Views.Store;

public sealed partial class ReadyKitViewPage : TemplatePage
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly Dictionary<string, double> _itemProgress = new(StringComparer.OrdinalIgnoreCase);

    private ReadyKitModel _kit;
    private List<RepoItemModel> _items = [];
    private bool _canInstall;
    private bool _hasInstallationError;
    private bool _handlersConnected;
    private bool _isPageActive;
    private bool _loaded;
    private string _activeItemId = string.Empty;
    private string _loadErrorTitle = string.Empty;
    private string _loadErrorMessage = string.Empty;
    private string _loadErrorCode = string.Empty;

    private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
    private Action<Tuple<string, ErrorModel>> _itemInstallingErrorHappensHandler;
    private Action<string> _itemActionsStoppedHandler;

    public MarkdownConfig MarkdownConfig { get; } = new();
    public ObservableCollection<ReadyKitContentItemViewModel> ContentItems { get; } = [];

    public ReadyKitViewPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = KitImage;

        Loaded += ReadyKitViewPage_Loaded;
    }

    private void ReadyKitViewPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
            ShowLoadErrorDialog();

        Loaded -= ReadyKitViewPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isPageActive = true;

        string kitId = Parameter?.Get("kitId");
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

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _isPageActive = false;
        DisconnectQueueHandlers();

        foreach (ReadyKitContentItemViewModel item in ContentItems)
            item.ImageSource = null;

        KitImage.Source = null;
    }

    private bool LoadKit()
    {
        _items = (_kit.items ?? [])
            .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
            .OfType<RepoItemModel>()
            .ToList();

        string locale = StoreLocalizationHelper.GetStoreLikeLocale();
        KitNameTextBlock.Text = _kit.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(
            _kit.name,
            locale);
        KitSmallDescriptionTextBlock.Text = LScriptLangHelper.ExecuteScript(_kit.small_description, locale);
        KitDescriptionTextBlock.Text = LScriptLangHelper.ExecuteScript(_kit.description, locale);
        KitImage.Source = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(_kit.icon)));
        KitIconBackground.Background = ReadyKitBrushFactory.Create(
            _items.Select(item => item.background),
            _kit.background);
        ContentsTitleTextBlock.Text = _localizer.GetLocalizedString("ReadyKitContents");
        ReadyKitsCategoryTextBlock.Text = _localizer.GetLocalizedString("ReadyKitsPageTitle");
        KitItemsCountTextBlock.Text = _items.Count.ToString();

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

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
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
            AcceptLicenseContentDialog dialog = new()
            {
                Licenses = licenses,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
                return;
        }

        _hasInstallationError = false;
        ErrorStatusGrid.Visibility = Visibility.Collapsed;
        foreach (RepoItemModel item in itemsToInstall)
            _itemProgress[item.store_id] = 0;

        ShowDownloadState();
        CurrentStatusTextBlock.Text = _localizer.GetLocalizedString("QueueWaiting");
        CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;

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

    private void StopActionButton_Click(object sender, RoutedEventArgs e)
    {
        StopActionButton.IsEnabled = false;
        CurrentStatusTextBlock.Text = _localizer.GetLocalizedString("Cancel");
        _hasInstallationError = false;

        foreach (RepoItemModel item in _items)
            StoreHelper.Instance.RemoveItemFromQueue(item.store_id);
    }

    private async void ErrorHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CDPIUI.Controls.Dialogs.Store.DownloadErrorActionsContentDialog
        {
            XamlRoot = XamlRoot
        };
        await dialog.ShowQueuedAsync();
    }

    private void LaunchKitUnsupportedHelp_Click(object sender, RoutedEventArgs e)
    {
        CommandsHandler.HandleCommand("cdpiui://Help/Store/ItemUnsupportedWarning/");
    }

    private void ContentItemButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not StoreItemSmallButton button)
            return;

        button.Click -= ContentItemButton_Click;
        button.Click += ContentItemButton_Click;
    }

    private void ContentItemButton_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is StoreItemSmallButton button)
            button.Click -= ContentItemButton_Click;
    }

    private void ContentItemButton_Click(StoreItemSmallButton button)
    {
        PrepareToConnectedForwardAnimate(button.imageElement);
        StoreWindow.Instance.NavigateSubPage(
            typeof(ItemViewPage),
            new NameValueCollection { { "itemId", button.StoreId } },
            new SuppressNavigationTransitionInfo());
    }

    private void ReadyKitsCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow.Instance.NavigateSubPage(
            typeof(ReadyKitsViewPage),
            null,
            new SuppressNavigationTransitionInfo());
    }

    private void DisableInstallation(string message)
    {
        _canInstall = false;
        InstallButtonTextBlock.Text = _localizer.GetLocalizedString("Get");
        InstallButton.IsEnabled = false;
        KitUnsupportedWarningTextBlock.Text = message;
        KitUnsupportedWarningGrid.Visibility = Visibility.Visible;
    }

    private void SetLoadError(string title, string message, string errorCode)
    {
        _canInstall = false;
        _loadErrorTitle = title;
        _loadErrorMessage = message;
        _loadErrorCode = errorCode;
    }

    private void ShowLoadErrorDialog()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = _loadErrorTitle,
                Content = $"{_loadErrorMessage} {_loadErrorCode}",
                PrimaryButtonText = _localizer.GetLocalizedString("OkButtonText")
            };
            await dialog.ShowQueuedAsync();

            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(HomePage));
        });
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
