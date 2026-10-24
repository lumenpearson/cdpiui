using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Views.Main.Components;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using CommunityToolkit.WinUI.Animations.Expressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.CDPIUIGenericHelpers;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store;

public sealed partial class ItemViewPage : TemplatePage
{
    private string _storeId;
    private RepoItemModel _item;

    private MarkdownConfig _config;

    public MarkdownConfig MarkdownConfig
    {
        get => _config;
        set => _config = value;
    }

    private bool _errorHappens = false;
    private bool _loaded = false;
    private bool _handlersConnected;
    private int _handlerVersion;
    private Action<string> _itemRemovedHandler;

    private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
    private Action<Tuple<string, TimeSpan>> _itemTimeRemainingChangedHandler;
    private Action<Tuple<string, ErrorModel>> _itemInstallingErrorHappensHandler;
    private Action<string> _itemActionsStoppedHandler;

    private ObservableCollection<AdditionalItemInfoModel> _additionalItemInfoModels = [];

    private ILocalizer _localizer = Localizer.Get();

    public ItemViewPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = ItemImage;

        _config = new MarkdownConfig();

        AdvancedInfoListView.ItemsSource = _additionalItemInfoModels;

        this.Loaded += ItemViewPage_Loaded;
    }

    private void ItemViewPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
            ShowErrorDialog();

        this.Loaded -= ItemViewPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);


        // support NameValueCollection or legacy string parameter
        if (Parameter != null && Parameter.Get("itemId") != null)
        {
            _storeId = Parameter.Get("itemId");

            ConnectHandlers();

            _item = StoreHelper.Instance.GetItemInfoFromStoreId(_storeId);

            if (_item == null || _storeId == SharedConstants.ApplicationStoreId)
            {
                return;
            }

            ItemName.Text = _item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(_item.name, StoreLocalizationHelper.GetStoreLikeLocale());
            ItemImage.Source = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(_item.icon)));
            ItemDeveloper.Text = _item.developer;
            Logger.Instance.CreateDebugLog(nameof(ItemViewPage), _item.category_id);
            StarCount.Text = _item.stars ?? "NaN";
            ItemCategoryButton.Content = StoreHelper.Instance.GetLocalizedStoreItemName(
                StoreHelper.Instance.GetCategoryFromStoreId(_item.category_id)?.name ?? string.Empty,
                StoreLocalizationHelper.GetStoreLikeLocale()
            );
            SmallDescriptionText.Text = LScriptLangHelper.ExecuteScript(_item.small_description, StoreLocalizationHelper.GetStoreLikeLocale());
            ItemFullDescriptionText.Text = LScriptLangHelper.ExecuteScript(_item.description, StoreLocalizationHelper.GetStoreLikeLocale());
            ItemWarningAera.Visibility = _item.display_warning ? Visibility.Visible : Visibility.Collapsed;
            ItemWarningText.Text = LScriptLangHelper.ExecuteScript(_item.warning_text, StoreLocalizationHelper.GetStoreLikeLocale());

            if (DatabaseHelper.Instance.IsItemInstalled(_storeId))
            {
                ItemActionButton.IsEnabled = _item.type == "component";
                ItemActionButtonText.Text = _localizer.GetLocalizedString("Setup");
                ItemMoreButton.Visibility = Visibility.Visible;
            }
            else
            {
                ItemActionButtonText.Text = _localizer.GetLocalizedString("Install");
                ItemMoreButton.Visibility = Visibility.Collapsed;

                bool isItemSupported = IsItemSupported();

                ItemActionButton.IsEnabled = isItemSupported;
                ItemUnsupportedWarningGrid.Visibility = isItemSupported ? Visibility.Collapsed : Visibility.Visible;
            }

            if (_item.links?.Count > 0)
                CreateLinks(_item.links);
            else
                LinksGrid.Visibility = Visibility.Collapsed;

            _loaded = true;

            LoadAdvancedItemInfo();
            CheckCurrentStatus();
        }
        else
        {
            ItemName.Text = "Template page";
        }
    }

    private void LoadAdvancedItemInfo()
    {
        _item = StoreHelper.Instance.GetItemInfoFromStoreId(_storeId);
        _additionalItemInfoModels.Clear();
        if (_item == null)
        {
            return;
        }
        foreach (AdditionalItemInfoModel info in AdditionalItemInfoBuilder.Build(_item))
            _additionalItemInfoModels.Add(info);
    }

    private bool IsItemSupported()
    {
        return VersionHelper.IsVersionInRange(ApplicationInfo.Version, _item.target_minversion, _item.target_maxversion);
    }

    private void ShowErrorDialog()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var dialog = new ContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Title = _localizer.GetLocalizedString("ElementNotExistTitle"),
                Content = string.Format(_localizer.GetLocalizedString("ElementNotExistMessage"), _storeId, "ERR_STORE_ITEM_NOT_FOUND"),
                PrimaryButtonText = _localizer.GetLocalizedString("OkButtonText")
            };
            await dialog.ShowQueuedAsync();
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(HomePage));
        });
    }

    private void CreateLinks(List<Link> links)
    {
        LinksStackPanel.Children.Clear();

        ObservableCollection<StoreItemLinkButton> linkButtons = new ObservableCollection<StoreItemLinkButton>();

        foreach (Link link in links)
        {
            linkButtons.Add(new StoreItemLinkButton
            {
                Text = StoreHelper.Instance.GetLocalizedStoreItemName(link.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                Url = link.url,
            });
        }

        var itemsControl = new ItemsControl
        {
            Margin = new Thickness(0, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = linkButtons,
            ItemsPanel = (ItemsPanelTemplate)this.Resources["DefaultItemsPanel"]
        };

        LinksStackPanel.Children.Add(itemsControl);

    }

    private async Task InstallingItemActions()
    {
        if (!await AskLicense())
            return;
        ConnectHandlers();
        _errorHappens = false;

        GetReadyUI();

        CurrentStatusTextBlock.Text = _localizer.GetLocalizedString("QueueWaiting");
        StoreHelper.Instance.AddItemToQueue(_storeId, string.Empty);

        ItemActionButton.IsEnabled = _item.type == "component";
    }

    private void ItemCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.CategoryViewPage), new NameValueCollection() { { "categoryId", _item.category_id } }, new SuppressNavigationTransitionInfo());
    }

    private async void ItemWarningButton_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new ContentDialog()
        {
            Title = _localizer.GetLocalizedString("MessageFromDeveloper"),
            Content = _localizer.GetLocalizedString("MessageFromDeveloperTip"),
            PrimaryButtonText = _localizer.GetLocalizedString("OkButtonText"),
            XamlRoot = this.XamlRoot,
        };
        await dialog.ShowQueuedAsync();
    }

    private async void ItemActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!DatabaseHelper.Instance.IsItemInstalled(_storeId))
        {
            await InstallingItemActions();
        }
        else
        {
            ModernMainWindow window = await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>();

            window.NavView_Navigate(typeof(ViewComponentSettingsPage),
                new NameValueCollection()
                {
                    { "componentId", _storeId }
                },
                new DrillInNavigationTransitionInfo());
        }
    }

    private void StopActionButton_Click(object sender, RoutedEventArgs e)
    {
        StoreHelper.Instance.RemoveItemFromQueue(_storeId);
    }

    private async void ErrorHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CDPIUI.Controls.Dialogs.Store.DownloadErrorActionsContentDialog
        {
            XamlRoot = XamlRoot
        };
        await dialog.ShowQueuedAsync();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {

        StopActionButton.IsEnabled = false;
        CurrentStatusTextBlock.Text = _localizer.GetLocalizedString("QueueWaiting");
        StatusProgressbar.IsIndeterminate = true;
        ErrorStatusGrid.Visibility = Visibility.Collapsed;
        ItemActionButton.Visibility = Visibility.Collapsed;
        DownloadStatusGrid.Visibility = Visibility.Visible;

        StoreHelper.Instance.RemoveItem(_storeId);

        ItemActionButton.IsEnabled = IsItemSupported();
    }

    private async void ReinstallButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: downloading from saved URL

        ContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot,
            Title = _localizer.GetLocalizedString("ReinstallWarningTitle"),
            Content = _localizer.GetLocalizedString("ReinstallWarningMessage"),
            PrimaryButtonText = _localizer.GetLocalizedString("Yes"),
            CloseButtonText = _localizer.GetLocalizedString("No"),
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
        {
            // The old files must be gone before the new ones are written.
            await StoreHelper.Instance.RemoveItemAsync(_storeId);
            await InstallingItemActions();
        }
    }

    private async Task<bool> AskLicense()
    {
        if (_item.license == null || _item.license.Count == 0)
            return true;

        AcceptLicenseContentDialog dialog = new()
        {
            Licenses = _item.license,
            XamlRoot = this.XamlRoot
        };
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void ShowFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellHelper.OpenFileInDefaultApp(DatabaseHelper.Instance.GetItemById(_item.store_id)?.Directory ?? string.Empty);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(ItemViewPage), exception.ToString());
        }
    }

    private void LaunchItemUnsupportedHelp_Click(object sender, RoutedEventArgs e)
    {
        Commands.CommandsHandler.HandleCommand(
            "cdpiui://Help/Store/ItemUnsupportedWarning/");
    }
}
