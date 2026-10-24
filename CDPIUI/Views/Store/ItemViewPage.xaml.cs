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

public class AdditionalItemInfoModel
{
    public string DisplayName { get; set; }
    public string DisplayValue { get; set; }
}
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
        _additionalItemInfoModels.Add(new() { DisplayName = "StoreId", DisplayValue = _item.store_id });
        _additionalItemInfoModels.Add(new() { DisplayName = "CategoryId", DisplayValue = _item.category_id });
        _additionalItemInfoModels.Add(new() { DisplayName = "BackgroundColor", DisplayValue = _item.background });
        _additionalItemInfoModels.Add(new() { DisplayName = "VersionControlType", DisplayValue = _item.version_control });
        if (_item.version_control == "several_repos")
        {
            int i = 0;
            if (_item.files_to_download != null)
            {
                foreach (var link in _item.files_to_download)
                {
                    i++;
                    _additionalItemInfoModels.Add(new() { DisplayName = $"#{i} > Type", DisplayValue = link.type });

                    _additionalItemInfoModels.Add(new() { DisplayName = $"#{i} > VersionControlType", DisplayValue = link.version_control });
                    _additionalItemInfoModels.Add(new() { DisplayName = $"#{i} > VersionControlLink", DisplayValue = link.version_control_link });

                    _additionalItemInfoModels.Add(new() { DisplayName = $"#{i} > PreferredVersion", DisplayValue = link.preffered_version });
                    _additionalItemInfoModels.Add(new() { DisplayName = $"#{i} > PreferredFileName", DisplayValue = link.preffered_to_download_file_name });

                    _additionalItemInfoModels.Add(new() { DisplayName = $"#{i} > DownloadLink", DisplayValue = link.download_link });
                    _additionalItemInfoModels.Add(new() { DisplayName = $"#{i} > ArchiveRootFolder", DisplayValue = link.archive_root_folder });
                }
            }
        }
        else
        {
            _additionalItemInfoModels.Add(new() { DisplayName = "VersionControlLink", DisplayValue = _item.version_control_link });
            _additionalItemInfoModels.Add(new() { DisplayName = "DownloadLink", DisplayValue = _item.download_link });
            _additionalItemInfoModels.Add(new() { DisplayName = "FileType", DisplayValue = _item.filetype });
            _additionalItemInfoModels.Add(new() { DisplayName = "ArchiveRootFolder", DisplayValue = _item.archive_root_folder });
        }


        _additionalItemInfoModels.Add(new() { DisplayName = "TargetExecutableFile", DisplayValue = _item.target_executable_file });

        _additionalItemInfoModels.Add(new() { DisplayName = "TargetMaxVer", DisplayValue = _item.target_maxversion });
        _additionalItemInfoModels.Add(new() { DisplayName = "TargetMinVer", DisplayValue = _item.target_minversion });
    }

    private void GetReadyUI()
    {
        StopActionButton.IsEnabled = true;
        StatusProgressbar.IsIndeterminate = true;
        ErrorStatusGrid.Visibility = Visibility.Collapsed;
        ItemActionButton.Visibility = Visibility.Collapsed;
        DownloadStatusGrid.Visibility = Visibility.Visible;
    }

    private void CheckCurrentStatus()
    {
        string operationId = StoreHelper.Instance.GetOperationIdFromItemId(_storeId);
        if (!string.IsNullOrEmpty(operationId))
        {
            GetReadyUI();

            string status = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadStage;
            PreferItemDownloadingStateActions(status ?? "QueueWaiting");
            StatusProgressbar.Value = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadProgress ?? 0;
        }
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

    private void PreferItemDownloadingStateActions(string state)
    {
        state ??= "QueueWaiting";
        string stageHeaderText = string.Empty;
        switch (state)
        {
            case "GETR":
                stageHeaderText = _localizer.GetLocalizedString("GettingReady");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "END":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "Downloading":
                stageHeaderText = _localizer.GetLocalizedString("Downloading");
                StatusProgressbar.IsIndeterminate = false;
                CurrentStatusSpeedTextBlock.Visibility = Visibility.Visible;
                break;
            case "Extracting":
                stageHeaderText = _localizer.GetLocalizedString("Installing");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "ErrorHappens":
                stageHeaderText = _localizer.GetLocalizedString("ErrorHappens");
                break;
            case "Completed":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "CANC":
                stageHeaderText = _localizer.GetLocalizedString("Cancel");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "ConnectingToService":
                stageHeaderText = _localizer.GetLocalizedString("ConnectingToService");
                CurrentStatusTipTextBlock.Text = _localizer.GetLocalizedString("ConnectingToServiceTip");
                StatusProgressbar.IsIndeterminate = true;
                break;
            default:
                stageHeaderText = _localizer.GetLocalizedString(state);
                StatusProgressbar.IsIndeterminate = true;
                break;
        }
        CurrentStatusTextBlock.Text = stageHeaderText;
    }

    private Action<Tuple<string, T>> ForCurrentOperation<T>(Action<Tuple<string, T>> action)
    {
        return data =>
        {
            // Resolve the operation before it is removed from the queue.
            if (StoreHelper.Instance.GetItemIdFromOperationId(data.Item1) != _storeId)
                return;
            int version = _handlerVersion;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_handlersConnected && version == _handlerVersion)
                    action(data);
            });
        };
    }

    private void ConnectHandlers()
    {
        if (_handlersConnected)
            return;
        _handlersConnected = true;
        ++_handlerVersion;
        _itemDownloadStageChangedHandler = ForCurrentOperation<string>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            string stage = data.Item2;

            GetReadyUI();

            CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;

            PreferItemDownloadingStateActions(stage);
        });
        StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

        _itemDownloadProgressChangedHandler = ForCurrentOperation<double>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            double progress = data.Item2;


            StatusProgressbar.IsIndeterminate = false;
            StatusProgressbar.Value = progress;
        });
        StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

        _itemTimeRemainingChangedHandler = ForCurrentOperation<TimeSpan>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            TimeSpan time = data.Item2;


            if (time.TotalMinutes >= 1)
                CurrentStatusTipTextBlock.Text = string.Format(_localizer.GetLocalizedString("/UIHelper/LeftMinutes"), (int)time.TotalMinutes);
            else
                CurrentStatusTipTextBlock.Text = _localizer.GetLocalizedString("/UIHelper/LeftSmall");
        });

        StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

        _itemDownloadSpeedChangedHandler = ForCurrentOperation<double>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            double speed = data.Item2;


            CurrentStatusSpeedTextBlock.Text = $"{UnitsParser.FormatSpeed(speed)}, ";
        });

        StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

        _itemInstallingErrorHappensHandler = ForCurrentOperation<ErrorModel>((data) =>
        {
            string operationId = data.Item1;
            string errorCode = data.Item2.ErrorCode;


            ErrorStatusGrid.Visibility = Visibility.Visible;
            ItemActionButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Collapsed;

            ErrorNameTextBlock.Text = errorCode;
            _errorHappens = true;
        });
        StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

        _itemActionsStoppedHandler = (id) =>
        {
            if (id != _storeId)
                return;
            int version = _handlerVersion;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_handlersConnected && version == _handlerVersion)
                    ShowItemAfterInstallActions(id);
            });
        };
        StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
        _itemRemovedHandler = id =>
        {
            if (id != _storeId)
                return;
            int version = _handlerVersion;
            DispatcherQueue.TryEnqueue(() =>
            {
                // Reinstall removes the old item before immediately queuing the replacement.
                if (_handlersConnected && version == _handlerVersion &&
                    StoreHelper.Instance.GetOperationIdFromItemId(_storeId) == null)
                {
                    ShowItemAfterInstallActions(id);
                }
            });
        };
        StoreHelper.Instance.ItemRemoved += _itemRemovedHandler;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _handlersConnected = false;
        ++_handlerVersion;
        StoreHelper.Instance.ItemRemoved -= _itemRemovedHandler;

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

        this.DataContext = null;
        MarkdownConfig = null;
    }

    private void ShowItemAfterInstallActions(string id)
    {
        if (id != _storeId)
            return;
        StatusProgressbar.Value = 0;
        StatusProgressbar.IsIndeterminate = true;

        CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;
        CurrentStatusSpeedTextBlock.Text = "";
        CurrentStatusTipTextBlock.Text = _localizer.GetLocalizedString("WorkingOnIt");

        if (id == _storeId && !_errorHappens)
        {
            ItemActionButton.IsEnabled = IsItemSupported();
            ItemActionButtonText.Text = _localizer.GetLocalizedString("Install");
            ItemMoreButton.Visibility = Visibility.Collapsed;
            ItemActionButton.Visibility = Visibility.Visible;
            DownloadStatusGrid.Visibility = Visibility.Collapsed;

            if (DatabaseHelper.Instance.IsItemInstalled(id))
            {
                ItemActionButtonText.Text = _localizer.GetLocalizedString("Setup");
                ItemMoreButton.Visibility = Visibility.Visible;

                ItemActionButton.IsEnabled = _item.type == "component";
            }
        }
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
