using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels.CreateConfig;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;

namespace CDPIUI.Views.CreateConfigHelper;

public sealed partial class EditConfigKitPage : TemplatePage, IEditConfigKitView
{
    private readonly ILocalizer _localizer = Localizer.Get();

    public EditConfigKitPage()
    {
        ViewModel = new EditConfigKitViewModel(this);
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    public EditConfigKitViewModel ViewModel { get; }

    public void SetStatus(bool isWorking, string text)
    {
        var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();
        window?.SetStatus(isWorking, text);
    }

    public async Task ShowErrorAsync(string message, string title)
    {
        try
        {
            var dlg = new MessageDialog(message, title);
            InitializeWithWindow.Initialize(
                dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>())
                );
            await dlg.ShowAsync();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(EditConfigKitPage), exception.ToString());
        }
    }

    public async Task ShowFileSaveErrorAsync(string content, string errorDetails)
    {
        ErrorContentDialog errorDialog = new() { };
        await errorDialog.ShowErrorDialogAsync(
            content: content,
            errorDetails: errorDetails,
            xamlRoot: this.Content.XamlRoot);
    }

    public async Task<SaveConfigKitDialogResultModel> AskSaveAsAsync(string displayName)
    {
        SaveConfigKitAsContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot,
            DisplayName = displayName,
        };

        await dialog.ShowQueuedAsync();

        return dialog.Result;
    }

    public string PickConfigPackFile(string fileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            OverwritePrompt = true,
            FileName = fileName,
            DefaultExt = ".cdpiconfigpack",
            Filter = $"{_localizer.GetLocalizedString("ConfigPack")}|*.cdpiconfigpack"
        };
        var result = dialog.ShowDialog();
        return result.HasValue && result.Value ? dialog.FileName : null;
    }

    public void OpenConfigEditor(NameValueCollection parameter) =>
        Frame.Navigate(typeof(CreateNewConfigPage), parameter, new DrillInNavigationTransitionInfo());

    public void LeaveAfterSave()
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(typeof(CreateConfigHelper.MainPage), new NameValueCollection(), new DrillInNavigationTransitionInfo());
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (e.SourcePageType != typeof(CreateNewConfigPage))
        {
            ViewModel.Clear();
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();

        if (window?.NavigateBackParameter is ConfigItem configItem)
        {
            ViewModel.ReplaceConfig(configItem);
        }
        window?.ClearNavigateBackParameter();

        if (IsLoaded)
            _ = LoadedActions();
        else
            Loaded += EditConfigKitPage_Loaded;
    }

    private void EditConfigKitPage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = LoadedActions();
        Loaded -= EditConfigKitPage_Loaded;
    }

    // LoadAsync catches and logs its own errors.
    private Task LoadedActions() =>
        Parameter != null ? ViewModel.LoadAsync(Parameter.Get("kitId")) : Task.CompletedTask;

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveConfirmationFlyout.Hide();
        try
        {
            await ViewModel.SaveKitAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(EditConfigKitPage), $"Cannot save the config kit: {ex}");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            CreateConfigHelperWindow.RemoveAndGoBackTo(typeof(MainPage), Frame);
        else
            ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>()?.Close();
    }

    private void CloseFlyoutButton_Click(object sender, RoutedEventArgs e)
    {
        SaveConfirmationFlyout.Hide();
    }

    private void SaveConfirmationFlyout_Opened(object sender, object e)
    {
        ViewModel.SetSaveEnabled(false);
    }

    private void SaveConfirmationFlyout_Closed(object sender, object e)
    {
        ViewModel.SetSaveEnabled(true);
    }
}
