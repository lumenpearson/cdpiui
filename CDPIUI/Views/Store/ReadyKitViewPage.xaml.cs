using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.ViewModels.Store;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;

namespace CDPIUI.Views.Store;

public sealed partial class ReadyKitViewPage : TemplatePage
{
    private readonly ILocalizer _localizer = Localizer.Get();

    public MarkdownConfig MarkdownConfig { get; } = new();

    public ReadyKitViewPageViewModel ViewModel { get; }

    public ReadyKitViewPage()
    {
        ViewModel = new ReadyKitViewPageViewModel(AcceptLicensesAsync, DispatcherQueue);

        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = KitImage;

        Loaded += ReadyKitViewPage_Loaded;
    }

    private void ReadyKitViewPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsLoaded)
            ShowLoadErrorDialog();

        Loaded -= ReadyKitViewPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Load(Parameter?.Get("kitId"));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Detach();
    }

    private async Task<bool> AcceptLicensesAsync(List<ItemLicenseModel> licenses)
    {
        AcceptLicenseContentDialog dialog = new()
        {
            Licenses = licenses,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    // Click handlers, not commands: the install and stop buttons bind IsEnabled.
    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.InstallAsync();
    }

    private void StopActionButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Stop();
    }

    private async void ErrorHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CDPIUI.Controls.Dialogs.Store.DownloadErrorActionsContentDialog
        {
            XamlRoot = XamlRoot
        };
        await dialog.ShowQueuedAsync();
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

    private void ShowLoadErrorDialog()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = ViewModel.LoadErrorTitle,
                Content = $"{ViewModel.LoadErrorMessage} {ViewModel.LoadErrorCode}",
                PrimaryButtonText = _localizer.GetLocalizedString("OkButtonText")
            };
            await dialog.ShowQueuedAsync();

            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(HomePage));
        });
    }
}
