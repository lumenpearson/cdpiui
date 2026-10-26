using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using CDPIUI.Default;
using CDPIUI.ViewModels.Store;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using WinUI3Localizer;

namespace CDPIUI.Messages;

/// <summary>
/// The window that installs a Store item from a pack file.
/// </summary>
public sealed partial class StoreLocalItemInstallingDialog : TemplateWindow
{
    private ILocalizer _localizer = Localizer.Get();

    public StoreLocalItemInstallingDialogViewModel ViewModel { get; }

    public StoreLocalItemInstallingDialog()
    {
        ViewModel = new StoreLocalItemInstallingDialogViewModel(DispatcherQueue);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeComponent();

        WindowTitle = _localizer.GetLocalizedString("StoreWindowsTitle");
        IconUri = @"Assets/Icons/Store.ico";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        DisableResizeFeature();

        ((App)Application.Current).OpenWindows.Add(this);

        this.Closed += StoreLocalItemInstallingDialog_Closed;
        this.Activated += StoreLocalItemInstallingDialog_Activated;
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // The button also has a localization uid, which a binding would compete with.
        if (e.PropertyName == nameof(StoreLocalItemInstallingDialogViewModel.CancelButtonText))
            CancelButton.Content = ViewModel.CancelButtonText;
    }

    private void StoreLocalItemInstallingDialog_Activated(object sender, WindowActivatedEventArgs args)
    {
        ((App)Application.Current).ShowWindowModalAsync(this);
        this.Activated -= StoreLocalItemInstallingDialog_Activated;
    }

    private void StoreLocalItemInstallingDialog_Closed(object sender, WindowEventArgs args)
    {
        ViewModel.Close();
    }

    public Task SetPackFilePath(string packFile) => ViewModel.SetPackFilePathAsync(packFile);

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel();
        this.Close();
    }

    private async void ViewInStoreButton_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        window.NavigateSubPage(typeof(Views.Store.ItemViewPage), new NameValueCollection() { { "itemId", ViewModel.StoreId } }, new SuppressNavigationTransitionInfo());
        this.Close();
    }

    // A click handler, not a command: the button binds IsEnabled.
    private async void GetButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.InstallAsync();
    }
}
