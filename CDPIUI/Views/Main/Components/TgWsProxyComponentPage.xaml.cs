using System;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels.ComponentViews;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI.Views.Main.Components;

public sealed partial class TgWsProxyComponentPage : TemplatePage, ITgWsProxyComponentView
{
    private readonly ILocalizer _localizer = Localizer.Get();

    public TgWsProxyComponentPage()
    {
        ViewModel = new TgWsProxyComponentViewModel(DispatcherQueue, this);

        InitializeComponent();
        this.DataContext = this;

        Loaded += PresetPage_Loaded;
        Unloaded += PresetPage_Unloaded;

        HideInfoBar();
    }

    public TgWsProxyComponentViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter != null && e.Parameter is ComponentPageNavigationModel model)
        {
            ViewModel.SetNavigationModel(model);
            EmptyPlaceholder.StoreId = ViewModel.ComponentId;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        PresetPage_Unloaded(this, null);
    }

    public async Task<TgWsDataCenterEdit> EditDataCenterAsync(string title, TgWsDataCenterEdit current)
    {
        var dialog = new TgWsDataCenterEditContentDialog()
        {
            XamlRoot = this.XamlRoot,
            Title = title
        };
        if (current != null)
        {
            dialog.Number = current.Number;
            dialog.Ip = current.Ip;
        }

        await dialog.ShowQueuedAsync();

        return dialog.Result ? new TgWsDataCenterEdit(dialog.Number, dialog.Ip) : null;
    }

    public async Task ShowErrorAsync(string message, string title)
    {
        var dlg = new MessageDialog(message, title);
        InitializeWithWindow.Initialize(
            dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>())
            );
        await dlg.ShowAsync();
    }

    public async Task<ConfigCreationChoice> AskConfigCreationAsync()
    {
        var dialog = new CreateNewConfigForComponentContentDialog()
        {
            XamlRoot = this.XamlRoot,
        };

        await dialog.ShowQueuedAsync();

        return dialog.Result switch
        {
            CreateNewConfigForComponentContentDialog.ConfigCreationVariants.FromTemplate => ConfigCreationChoice.FromTemplate,
            CreateNewConfigForComponentContentDialog.ConfigCreationVariants.Manual => ConfigCreationChoice.Manual,
            _ => ConfigCreationChoice.None,
        };
    }

    public Task CreateConfigInEditorAsync(string componentId) => ConfigMakerWindow.CreateForComponentAsync(componentId);

    public Task EditConfigAsync(ConfigItem item) => ConfigMakerWindow.EditConfig(item);

    public async Task ShowConnectTelegramProxyAsync()
    {
        ConnectTelegramProxyContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot,
        };
        await dialog.ShowQueuedAsync();
    }

    private void PresetPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Load();
    }

    private void PresetPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Unload();
    }

    private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.SelectConfigAsync(ConfigChooseCombobox.SelectedItem as ConfigSelectorItem, IsLoaded);
    }

    private void ShowInfoBar(string title, InfoBarSeverity severity)
    {
        HideInfoBar();
        ActionInfoBar.IsOpen = true;
        ActionInfoBar.Title = title;
        ActionInfoBar.Severity = severity;
        HideInfoBarAnimation.Stop();
        ShowInfoBarAnimation.Begin();
    }

    private void HideInfoBar()
    {
        ShowInfoBarAnimation.Stop();
        HideInfoBarAnimation.Begin();
    }

    private void ActionInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        HideInfoBar();
        args.Cancel = true;
    }

    private async void PasteFromBufferButton_Click(object sender, RoutedEventArgs e)
    {
        DataPackageView dataPackageView = Clipboard.GetContent();

        if (dataPackageView.Contains(StandardDataFormats.Text) is true)
        {
            string text = await dataPackageView.GetTextAsync();

            if (ViewModel.PasteDataCenters(text))
            {
                ShowInfoBar(_localizer.GetLocalizedString("PasteComplete"), InfoBarSeverity.Success);
                return;
            }
            ShowInfoBar(_localizer.GetLocalizedString("PasteFailure"), InfoBarSeverity.Warning);
        }
    }

    private void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        DataPackage dataPackage = new();
        dataPackage.RequestedOperation = DataPackageOperation.Copy;
        dataPackage.SetText(ViewModel.BuildShareText());
        Clipboard.SetContent(dataPackage);

        ShowInfoBar(_localizer.GetLocalizedString("CopyComplete"), InfoBarSeverity.Success);
    }
}
