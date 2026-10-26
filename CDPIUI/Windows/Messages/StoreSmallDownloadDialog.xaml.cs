using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Default;
using CDPIUI.ViewModels.Store;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using WinUI3Localizer;

namespace CDPIUI.Messages;

public sealed partial class StoreSmallDownloadDialog : TemplateWindow
{
    private ILocalizer _localizer = Localizer.Get();

    public StoreSmallDownloadDialogViewModel ViewModel { get; }

    public StoreSmallDownloadDialog()
    {
        ViewModel = new StoreSmallDownloadDialogViewModel(AcceptLicensesAsync, DispatcherQueue);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeComponent();

        NewIdSet += NewIdSeted;

        WindowTitle = _localizer.GetLocalizedString("StoreWindowsTitle");
        IconUri = @"Assets/Icons/Store.ico";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        DisableResizeFeature();

        this.Closed += StoreSmallDownloadDialog_Closed;
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // The button also has a localization uid, which a binding would compete with.
        if (e.PropertyName == nameof(StoreSmallDownloadDialogViewModel.CancelButtonText))
            CancelButton.Content = ViewModel.CancelButtonText;
    }

    private void NewIdSeted()
    {
        _ = SetItemToViewId(Id); // SetItemToViewId catches and logs its own errors.
    }

    private void StoreSmallDownloadDialog_Closed(object sender, WindowEventArgs args)
    {
        NewIdSet -= NewIdSeted;
        ViewModel.Detach();
    }

    public Task SetItemToViewId(string id) => ViewModel.SetItemToViewIdAsync(id);

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel();
        this.Close();
    }

    private void GetButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.StartDownloadAsync(); // StartDownloadAsync catches and logs its own errors.
    }

    private async void ViewInStoreButton_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        window.NavigateSubPage(typeof(Views.Store.ItemViewPage), new NameValueCollection() { { "itemId", ViewModel.StoreId } }, new SuppressNavigationTransitionInfo());
        this.Close();
    }

    private async Task<bool> AcceptLicensesAsync(List<ItemLicenseModel> licenses)
    {
        AcceptLicenseContentDialog dialog = new()
        {
            Licenses = licenses,
            XamlRoot = this.Content.XamlRoot
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }
}
