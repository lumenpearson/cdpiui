using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.ProxySetupUtil;
using CDPIUI.ViewModels.Proxy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace CDPIUI.Views.SetupProxy;

public sealed partial class ProxiFyreSetupPage : TemplatePage, IProxiFyreSetupView
{
    public ProxiFyreSetupPage()
    {
        InitializeComponent();
        this.DataContext = this;

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = ActionButtonsGrid;

        ViewModel = new ProxiFyreSetupViewModel(this);
    }

    public ProxiFyreSetupViewModel ViewModel { get; }

    public Task<bool> ConfirmProxyAddressAsync(string ip) =>
        global::CDPIUI.Helper.Basic.ProxyAddressWarning.ConfirmAsync(XamlRoot, ip);

    public async Task<List<SelectedApplicationInfo>> EditApplicationsAsync(List<SelectedApplicationInfo> applications, string title)
    {
        SelectAppContentDialog dialog = new SelectAppContentDialog(applications);
        dialog.XamlRoot = this.XamlRoot;
        dialog.Title = title;
        await dialog.ShowQueuedAsync();

        return dialog.Applications.ToList();
    }

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        PrepareToConnectedBackwardAnimate(ActionButtonsGrid, new DirectConnectedAnimationConfiguration());
        if (Frame.CanGoBack)
            Frame.GoBack();
    }

    private void Navigate<T>() where T : Page
    {
        PrepareToConnectedForwardAnimate(ActionButtonsGrid, new DirectConnectedAnimationConfiguration());

        Frame.Navigate(typeof(T), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open help link
    }

    private async void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.ConfirmAndSaveAsync())
            Navigate<ProxySetupCompletePage>();
    }
}
