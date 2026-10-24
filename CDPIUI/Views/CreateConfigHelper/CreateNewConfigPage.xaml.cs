using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TextControlBoxNS;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;

namespace CDPIUI.Views.CreateConfigHelper;

public sealed partial class CreateNewConfigPage : TemplatePage
{
    private List<string> _designerSupportedComponentIds = ["CSSIXC048", "CSGIVS036", "CSNIG9025"];

    private class ComponentModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public ObservableCollection<ConditionVariableModel> Conditions { get; } = new ObservableCollection<ConditionVariableModel>();
    public ObservableCollection<VariableModel> Variables { get; } = new ObservableCollection<VariableModel>();

    public ICommand RemoveConditionCommand { get; }
    public ICommand RemoveVariableCommand { get; }
    public ICommand VariableChangedCommand { get; }

    public ICommand GraphicDesignerTextValueChangedCommand { get; }
    public ICommand GraphicDesignerBoolValueToggledCommand { get; }
    public ICommand GraphicDesignerSelectedGuidChangedCommand { get; }
    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels { get; } = [];
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels { get; } = [];

    private ConfigItem _configItem;

    private ILocalizer _localizer = Localizer.Get();

    public string EditItemId = string.Empty;
    public PageOpenModes PageOpenMode = PageOpenModes.None;

    private bool _isRunning = false;
    private string _curRunId = string.Empty;
    private bool _isOpened = true;
    private string _listDirectory = string.Empty;
    private string _binDirectory = string.Empty;
    private string _nowConfigLoadedId = string.Empty;

    public CreateNewConfigPage()
    {
        InitializeComponent();
        this.DataContext = this;
        this.Loaded += CreateNewConfigPage_Loaded;
        RemoveConditionCommand = new RelayCommand(p => RemoveCondition(p));
        RemoveVariableCommand = new RelayCommand(p => RemoveVariable(p));
        VariableChangedCommand = new RelayCommand(p => VariableChangedValue((Tuple<string, string>)p));

        ConditionsListView.ItemsSource = Conditions;
        VariablesListView.ItemsSource = Variables;

        GraphicDesignerTextValueChangedCommand = new RelayCommand(p => HandleGraphicDesignerTextValueChanged((Tuple<string, string>)p));
        GraphicDesignerBoolValueToggledCommand = new RelayCommand(p => HandleGraphicDesignerBoolValueToggled((Tuple<string, bool>)p));
        GraphicDesignerSelectedGuidChangedCommand = new RelayCommand(p => HandleGraphicDesignerSelectedGuidChanged((Tuple<string, string>)p));

        GUIDesignerListView.ItemsSource = DesignerSettingItemModels;
        GUIDesignerExclusiveListView.ItemsSource = DesignerExclusiveSettingItemModels;

        CheckHighlight();

        StartupStringTextBox.UseSpacesInsteadTabs = true;
        StartupStringTextBox.NumberOfSpacesForTab = 4;

        InitPage();

        this.ActualThemeChanged += CreateNewConfigPage_ActualThemeChanged;
        this.ProcessKeyboardAccelerators += CreateNewConfigPage_ProcessKeyboardAccelerators;
        this.KeyDown += CreateNewConfigPage_KeyDown;
    }

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

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        this.KeyDown -= CreateNewConfigPage_KeyDown;
        this.ProcessKeyboardAccelerators -= CreateNewConfigPage_ProcessKeyboardAccelerators;
        this.ActualThemeChanged -= CreateNewConfigPage_ActualThemeChanged;
        StopTestRun();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    private void CreateNewConfigPage_Loaded(object sender, RoutedEventArgs e)
    {
        EditItemId = SharedConstants.LocalUserItemsId;

        if (Parameter != null)
            RunOnNavigatedToActions(Parameter);
        AuditSaveAvailable();
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

    private void InitPage()
    {
        List<ComponentModel> components = new();
        foreach (var component in HardcodedItemIds.ComponentIds)
        {
            components.Add(new()
            {
                Id = component.Value,
                Name = component.Key.ToString()
            });
        }
        ComponentChooseComboBox.ItemsSource = components;

        AuditSaveAvailable();

        SaveButtonText.Text = _localizer.GetLocalizedString("Save");
        PageTitleTextBlock.Text = _localizer.GetLocalizedString("CreateNewConfig");
        TestButtonText.Text = _localizer.GetLocalizedString("TestThis");
        ExpandToolTip.Content = _localizer.GetLocalizedString("HideConfigSettings");
    }

    private bool IsSaveAvailable()
    {
        bool result = false;

        result = ComponentChooseComboBox.SelectedIndex != -1 &&
            !string.IsNullOrEmpty(DisplayNameTextBox.Text) &&
            !string.IsNullOrEmpty(StartupStringTextBox.Text);
        return result;
    }

    private void AuditSaveAvailable()
    {
        bool state = IsSaveAvailable();
        SaveConfigButton.IsEnabled = state;
        TestButtonText.IsEnabled = state;
    }

    private bool IsSearchAvailable()
    {
        if (_designerSupportedComponentIds.Contains(((ComponentModel)ComponentChooseComboBox.SelectedItem)?.Id ?? ""))
            return MainPivot.SelectedItem == DefaultDesigner;
        else
            return true;
    }

    private void CheckViewType()
    {
        if (ComponentChooseComboBox.SelectedItem == null)
        {
            AdditionalSettingsPanel.Visibility = Visibility.Collapsed;
            NotReadyErrorGrid.Visibility = Visibility.Visible;
            return;
        }
        else
        {
            AdditionalSettingsPanel.Visibility = Visibility.Visible;
            NotReadyErrorGrid.Visibility = Visibility.Collapsed;
        }
        if (_designerSupportedComponentIds.Contains(((ComponentModel)ComponentChooseComboBox.SelectedItem).Id))
        {
            DesignerItem.IsSelected = true;
            MainPivot.Visibility = Visibility.Visible;
            HandleSelectionChange(false);
        }
        else
        {
            HandleSelectionChange(true);
            DefaultDesigner.IsSelected = true;
            MainPivot.Visibility = Visibility.Collapsed;
        }
    }

    private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CheckViewType();

        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
    }

    private void DisplayNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
    }

    private void StartupStringTextBox_TextChanged(TextControlBox sender)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
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

    private async Task ShowErrorDialog(string message, string title)
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
}
