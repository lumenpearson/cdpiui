using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels.CreateConfig;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using TextControlBoxNS;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;

namespace CDPIUI.Views.CreateConfigHelper;

public sealed partial class CreateNewConfigPage : TemplatePage, ICreateNewConfigView
{
    private readonly ILocalizer _localizer = Localizer.Get();

    private bool _isOpened = true;

    public CreateNewConfigPage()
    {
        ViewModel = new CreateNewConfigViewModel(this);
        InitializeComponent();
        this.Loaded += CreateNewConfigPage_Loaded;

        CheckHighlight();

        StartupStringTextBox.UseSpacesInsteadTabs = true;
        StartupStringTextBox.NumberOfSpacesForTab = 4;

        ExpandToolTip.Content = _localizer.GetLocalizedString("HideConfigSettings");

        this.ActualThemeChanged += CreateNewConfigPage_ActualThemeChanged;
        this.ProcessKeyboardAccelerators += CreateNewConfigPage_ProcessKeyboardAccelerators;
        this.KeyDown += CreateNewConfigPage_KeyDown;
    }

    public CreateNewConfigViewModel ViewModel { get; }

    public string EditorText
    {
        get => StartupStringTextBox.Text;
        set => StartupStringTextBox.Text = value;
    }

    public bool IsTextEditorTabSelected => MainPivot.SelectedItem == DefaultDesigner;

    public void OpenSearch()
    {
        if (IsSearchAvailable())
            SearchControl.ShowSearch(StartupStringTextBox);
    }

    public void OpenSearchAndReplace()
    {
        if (IsSearchAvailable())
            SearchControl.ShowReplace(StartupStringTextBox);
    }

    public void ChangeZoom(int zoom)
    {
        if (zoom == 0)
            StartupStringTextBox.ZoomFactor = 100;
        else
            StartupStringTextBox.ZoomFactor += zoom;
    }

    public void ChangeBackground(bool showDefaultOnly = false)
    {
        Style style;
        string background = SettingsManager.Instance.GetValue<string>("APPEARANCE", "configEditorBackground");
        if (MainPivot.SelectedItem == DefaultDesigner || showDefaultOnly || MainPivot.SelectedItem == null)
        {
            style = background switch
            {
                "MicaSmoke" => (Style)Resources["MicaSmokeStyle"],
                "Mica" => (Style)Resources["MicaStyle"],
                "MicaTransparent" => (Style)Resources["TransparentStyle"],
                _ => (Style)Resources["BlackStyle"],
            };
        }
        else
        {
            style = (Style)Resources["GrayStyle"];
        }

        if (style == (Style)Resources["BlackStyle"] || style == (Style)Resources["MicaSmokeStyle"])
        {
            CheckHighlight(ElementTheme.Dark);
        }
        else
        {
            CheckHighlight(ElementTheme.Default);
        }

        MainGrid.Style = style;
    }

    public void ResetEditorCursor() => StartupStringTextBox.SetCursorPosition(0, 0);

    public void SelectDesignerTab() => DesignerItem.IsSelected = true;

    public void SelectTextEditorTab() => DefaultDesigner.IsSelected = true;

    public void ApplyEditorBackground() => ChangeBackground();

    public void CloseSearch() => SearchControl.Close();

    public void SetExitAskAvailable(bool value) =>
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = value;

    public async Task<Dictionary<string, string>> SelectUsedFilesAsync(List<string> usedFiles, ConfigItem configItem, string directory, AskAutoFillMode mode)
    {
        SelectUsedFilesForConfigContentDialog dialog = new(usedFiles, configItem.name, directory, configItem, mode)
        {
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowQueuedAsync();
        return dialog.Result == CreateConfigResult.Selected ? dialog.Files : null;
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
            Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), exception.ToString());
        }
    }

    public async Task ShowSaveCompletedAsync()
    {
        CreateCompleteDialog dialog = new()
        {
            XamlRoot = this.XamlRoot
        };
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
            CreateConfigHelperWindow.Instanse.Close();
        else
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }
    }

    public void NavigateToMainPage() =>
        Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());

    public void ReturnEditedConfig(ConfigItem configItem)
    {
        var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();
        if (window != null)
            window.IsOperationExitAskAvailable = false;
        window?.NavigateBackWithParameter(configItem);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        this.KeyDown -= CreateNewConfigPage_KeyDown;
        this.ProcessKeyboardAccelerators -= CreateNewConfigPage_ProcessKeyboardAccelerators;
        this.ActualThemeChanged -= CreateNewConfigPage_ActualThemeChanged;
        ViewModel.StopTestRun();
    }

    private void CreateNewConfigPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Open(Parameter);
        Parameter = null;
        this.Loaded -= CreateNewConfigPage_Loaded;
    }

    private void CreateNewConfigPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            SearchControl.Close();
        }
    }

    private void CreateNewConfigPage_ProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
    {
        if ((args.Key == Windows.System.VirtualKey.Add || args.Key.ToString() == "187") && args.Modifiers == Windows.System.VirtualKeyModifiers.Control)
        {
            ChangeZoom(5);
        }
        else if ((args.Key == Windows.System.VirtualKey.Subtract || args.Key.ToString() == "189") && args.Modifiers == Windows.System.VirtualKeyModifiers.Control)
        {
            ChangeZoom(-5);
        }
    }

    private void CreateNewConfigPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (MainGrid.Style == (Style)Resources["BlackStyle"] || MainGrid.Style == (Style)Resources["MicaSmokeStyle"])
        {
            CheckHighlight(ElementTheme.Dark);
        }
        else
        {
            CheckHighlight(ElementTheme.Default);
        }
    }

    private void CheckHighlight(ElementTheme elementTheme)
    {
        if (elementTheme == ElementTheme.Light || (elementTheme == ElementTheme.Default && ((App)Application.Current).CurrentTheme == ElementTheme.Light))
        {
            StartupStringTextBox.SyntaxHighlighting = new LightDefaultHighlighter();
            StartupStringTextBox.Design = TextControlBoxDesigns.DefaultLightDesign;
        }
        else
        {
            StartupStringTextBox.SyntaxHighlighting = new DarkDefaultHighlighter();
            StartupStringTextBox.Design = TextControlBoxDesigns.DefaultDarkDesign;
        }
    }

    private void CheckHighlight()
    {
        CheckHighlight(MainGrid.RequestedTheme);
    }

    private bool IsSearchAvailable()
    {
        if (ViewModel.IsDesignerSupported)
            return MainPivot.SelectedItem == DefaultDesigner;
        else
            return true;
    }

    private void MainPivot_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        ViewModel.OnEditorTabChanged();
    }

    private void StartupStringTextBox_TextChanged(TextControlBox sender)
    {
        ViewModel.OnEditorTextChanged();
    }

    private void ConditionFlyout_Opened(object sender, object e)
    {
        ViewModel.ResetConditionDraft();
    }

    private void SaveConditionButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddCondition();
        ConditionFlyout.Hide();
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isOpened)
        {
            ShowInfoPanel.Begin();
            HideInfoPanel.Stop();
            InfoPanelDefinition.Width = new GridLength(350);
            ShowButtonFontIcon.Glyph = "\uE76C";
            ExpandToolTip.Content = _localizer.GetLocalizedString("HideConfigSettings");
        }
        else
        {
            ShowInfoPanel.Stop();
            HideInfoPanel.Begin();
            InfoPanelDefinition.Width = new GridLength(0);
            ShowButtonFontIcon.Glyph = "\uE76B";
            ExpandToolTip.Content = _localizer.GetLocalizedString("ShowConfigSettings");
        }
        _isOpened = !_isOpened;
    }
}
