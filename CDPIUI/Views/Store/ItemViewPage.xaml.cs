using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.ViewModels.Store;
using CDPIUI.Views.Main.Components;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;

namespace CDPIUI.Views.Store;

public sealed partial class ItemViewPage : TemplatePage, IItemViewPageView
{
    private MarkdownConfig _config;

    public MarkdownConfig MarkdownConfig
    {
        get => _config;
        set => _config = value;
    }

    private ILocalizer _localizer = Localizer.Get();

    public ItemViewPageViewModel ViewModel { get; }

    public ItemViewPage()
    {
        ViewModel = new ItemViewPageViewModel(this, DispatcherQueue);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = ItemImage;

        _config = new MarkdownConfig();

        this.Loaded += ItemViewPage_Loaded;
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // The tip text block also has a localization uid, which a binding would compete with.
        if (e.PropertyName == nameof(ItemViewPageViewModel.StatusTip))
            CurrentStatusTipTextBlock.Text = ViewModel.StatusTip;
    }

    private void ItemViewPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsLoaded)
            ShowErrorDialog();

        this.Loaded -= ItemViewPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // support NameValueCollection or legacy string parameter
        if (Parameter != null && Parameter.Get("itemId") != null)
            ViewModel.Load(Parameter.Get("itemId"));
        else
            ViewModel.ShowTemplate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Detach();

        this.DataContext = null;
        MarkdownConfig = null;
    }

    private void ShowErrorDialog()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var dialog = new ContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Title = _localizer.GetLocalizedString("ElementNotExistTitle"),
                Content = string.Format(_localizer.GetLocalizedString("ElementNotExistMessage"), ViewModel.StoreId, "ERR_STORE_ITEM_NOT_FOUND"),
                PrimaryButtonText = _localizer.GetLocalizedString("OkButtonText")
            };
            await dialog.ShowQueuedAsync();
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(HomePage));
        });
    }

    public void ShowLinks(List<Link> links)
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

    public async Task<bool> AcceptLicensesAsync(List<ItemLicenseModel> licenses)
    {
        AcceptLicenseContentDialog dialog = new()
        {
            Licenses = licenses,
            XamlRoot = this.XamlRoot
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmReinstallAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot,
            Title = _localizer.GetLocalizedString("ReinstallWarningTitle"),
            Content = _localizer.GetLocalizedString("ReinstallWarningMessage"),
            PrimaryButtonText = _localizer.GetLocalizedString("Yes"),
            CloseButtonText = _localizer.GetLocalizedString("No"),
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    public async Task OpenComponentSettingsAsync(string componentId)
    {
        ModernMainWindow window = await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>();

        window.NavView_Navigate(typeof(ViewComponentSettingsPage),
            new NameValueCollection()
            {
                { "componentId", componentId }
            },
            new DrillInNavigationTransitionInfo());
    }

    private void ItemCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.CategoryViewPage), new NameValueCollection() { { "categoryId", ViewModel.CategoryId } }, new SuppressNavigationTransitionInfo());
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

    // Click handlers, not commands: these buttons bind IsEnabled, and the old handlers
    // could run again while a dialog of an earlier click was open.
    private async void ItemActionButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunItemActionAsync();
    }

    private void StopActionButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StopAction();
    }

    private async void ErrorHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CDPIUI.Controls.Dialogs.Store.DownloadErrorActionsContentDialog
        {
            XamlRoot = XamlRoot
        };
        await dialog.ShowQueuedAsync();
    }

    private async void ReinstallButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ReinstallAsync();
    }
}
