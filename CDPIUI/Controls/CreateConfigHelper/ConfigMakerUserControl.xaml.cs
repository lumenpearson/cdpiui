using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.Universal;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed partial class ConfigMakerUserControl : UserControl
{
    public static readonly DependencyProperty ComponentIdProperty = DependencyProperty.Register(
        nameof(ComponentId),
        typeof(string),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(string.Empty, OnComponentIdChanged));

    public static readonly DependencyProperty CommandTextProperty = DependencyProperty.Register(
        nameof(CommandText),
        typeof(string),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(string.Empty, OnCommandTextChanged));

    public static readonly DependencyProperty IsEditorReadOnlyProperty = DependencyProperty.Register(
        nameof(IsEditorReadOnly),
        typeof(bool),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(false, OnIsEditorReadOnlyChanged));

    public static readonly DependencyProperty IsErrorCheckEnabledProperty = DependencyProperty.Register(
        nameof(IsErrorCheckEnabled),
        typeof(bool),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(true, OnIsErrorCheckEnabled));

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly ComponentCommandHelpService _helpService = new();
    private readonly ConfigMakerPresetDocument _presetDocument = new();
    private readonly ConfigMakerPresetResources _presetResources;
    private readonly Zapret2PresetOptionDescriber _zapret2OptionDescriber;
    private ComponentCommandHelpDocument _helpDocument = new();
    private CancellationTokenSource _helpCancellation;
    private ConfigMakerCommandOptionViewModel _selectedCommand;
    private bool _updatingComponent;
    private bool _updatingEditor;
    private bool _isTesting;
    private bool _isStartingTest;
    private bool _restoreComponentAfterTest;
    private string _testComponentId = string.Empty;
    private bool _layoutInitialized;
    private bool _updatingLayoutBounds;
    private bool _hasPresetFiles;
    private bool _hasPresetGroups;
    private bool _usesExplicitPresetStructure;
    private ConfigMakerPresetFileInfo[] _explicitPresetFiles = [];
    private ConfigMakerPresetGroupInfo[] _explicitPresetGroups = [];
    private double _savedEditorWidth = 620;
    private double _savedEditorHeight = 420;
    private string _highlightSignature = string.Empty;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _diagnosticsTimer;
    private string _editorBackground = "Black";
    private bool _updatingDesigner;
    private bool _designerNeedsRefresh = true;

    public ICommand ZoomIn { get; }
    public ICommand ZoomOut { get; }
    public ICommand ZoomReset { get; }
    public ICommand Search { get; }
    public ICommand Replace { get; }
    public ICommand DesignerTextValueChangedCommand { get; }
    public ICommand DesignerBoolValueToggledCommand { get; }
    public ICommand DesignerSelectedGuidChangedCommand { get; }

    public ConfigMakerUserControl()
    {
        _presetResources = new ConfigMakerPresetResources(
            _presetDocument,
            () => ComponentId,
            () => CommandText);
        _zapret2OptionDescriber = new Zapret2PresetOptionDescriber(_localizer);
        DesignerTextValueChangedCommand = new RelayCommand(parameter =>
            HandleDesignerTextValueChanged((Tuple<string, string>)parameter));
        DesignerBoolValueToggledCommand = new RelayCommand(parameter =>
            HandleDesignerBoolValueToggled((Tuple<string, bool>)parameter));
        DesignerSelectedGuidChangedCommand = new RelayCommand(parameter =>
            HandleDesignerSelectedGuidChanged((Tuple<string, string>)parameter));
        InitializeComponent();

        ZoomIn = new RelayCommand(p => ChangeZoom(5));
        ZoomOut = new RelayCommand(p => ChangeZoom(-5));
        ZoomReset = new RelayCommand(p => ChangeZoom(0));

        Search = new RelayCommand(p => EditorSearchControl.ShowSearch(CommandEditor));
        Replace = new RelayCommand(p => EditorSearchControl.ShowReplace(CommandEditor));

        CommandEditor.UseSpacesInsteadTabs = true;
        CommandEditor.NumberOfSpacesForTab = 4;
        _diagnosticsTimer = DispatcherQueue.CreateTimer();
        _diagnosticsTimer.Interval = TimeSpan.FromMilliseconds(300);
        _diagnosticsTimer.IsRepeating = false;
        _diagnosticsTimer.Tick += DiagnosticsTimer_Tick;
        _presetDocument.ContentChanged += PresetDocument_ContentChanged;
        ApplyEditorTheme();

        Loaded += ConfigMakerUserControl_Loaded;
        Unloaded += ConfigMakerUserControl_Unloaded;
        ActualThemeChanged += ConfigMakerUserControl_ActualThemeChanged;
    }

    public ObservableCollection<ConfigMakerCommandOptionViewModel> CommandOptions { get; } = [];
    public ObservableCollection<ConfigMakerCommandModuleViewModel> CommandModules { get; } = [];
    public ObservableCollection<ConfigMakerDiagnosticViewModel> Diagnostics { get; } = [];
    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels { get; } = [];
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels { get; } = [];
    public ObservableCollection<ConfigMakerVariableDefinition> PresetVariables => _presetDocument.Variables;
    public ObservableCollection<ConfigMakerPresetResource> PresetResources => _presetDocument.Resources;
    public ConfigMakerPresetDocument PresetDocument => _presetDocument;

    public string ComponentId
    {
        get => (string)GetValue(ComponentIdProperty);
        set => SetValue(ComponentIdProperty, value);
    }

    public string CommandText
    {
        get => (string)GetValue(CommandTextProperty);
        set => SetValue(CommandTextProperty, value);
    }

    public bool IsEditorReadOnly
    {
        get => (bool)GetValue(IsEditorReadOnlyProperty);
        set => SetValue(IsEditorReadOnlyProperty, value);
    }

    public bool IsErrorCheckEnabled
    {
        get => (bool)GetValue(IsErrorCheckEnabledProperty);
        set => SetValue(IsErrorCheckEnabledProperty, value);
    }

    public bool UseInlineStatusMessages { get; set; } = true;

    public event Action<string> CommandTextChanged;
    public event EventHandler TestStateChanged;
    public event EventHandler EditorReadOnlyChanged;
    public event EventHandler PanelStateChanged;
    public event EventHandler<ConfigMakerPresetFileReplacedEventArgs> PresetFileReplaced;
    public event EventHandler<StatusNotificationRequestedEventArgs> StatusNotificationRequested;
    public event EventHandler DocumentStateChanged;

    public bool IsTesting => _isTesting;
    public bool IsCommandPanelVisible => CommandPanel.Visibility == Visibility.Visible;
    public bool IsBottomPanelVisible => BottomPanel.Visibility == Visibility.Visible;
    public bool IsPresetFilesPanelVisible => PresetFilesPanel.Visibility == Visibility.Visible;
    public bool IsPresetStructureVisible => _hasPresetFiles || HasPresetGroups;
    public bool HasPresetFiles => _hasPresetFiles;
    public bool HasPresetGroups => _hasPresetGroups || !_usesExplicitPresetStructure;
    public bool HasVariables => _presetDocument.HasVariables;
    public bool CanExportText => !HasVariables;
    public bool IsSimpleDesignerSupported => IsSimpleDesignerComponent(ComponentId);

    public async Task SetComponentAsync(string componentId)
    {
        componentId = componentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return;
        }

        if (_isTesting && !string.Equals(_testComponentId, componentId, StringComparison.OrdinalIgnoreCase))
        {
            await StopTestAsync();
        }

        _updatingComponent = true;
        ComponentId = componentId;
        _updatingComponent = false;
        _presetDocument.ComponentId = componentId;
        ConfigOutput.ComponentId = componentId;
        UpdateSimpleDesignerAvailability();
        RebuildPresetStructure();
        await LoadHelpAsync(componentId, forceRefresh: false);
    }

    private static void OnIsEditorReadOnlyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        ConfigMakerUserControl control = (ConfigMakerUserControl)dependencyObject;
        bool isReadOnly = (bool)args.NewValue;
        control.ReplaceEditorButton.IsEnabled = !isReadOnly;
        control.InsertButton.IsEnabled = !isReadOnly && control._selectedCommand != null;
        control.EditorReadOnlyChanged?.Invoke(control, EventArgs.Empty);
        control.EditorSearchControl.ReplaceEnable = !isReadOnly;
    }

    private static void OnIsErrorCheckEnabled(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        // pass
    }

    private static void OnComponentIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ConfigMakerUserControl control)
        {
            return;
        }
        control._presetDocument.ComponentId = (string)args.NewValue;
        control._designerNeedsRefresh = true;
        control.UpdateSimpleDesignerAvailability();
        if (!control._updatingComponent && control.IsLoaded)
        {
            _ = control.SetComponentAsync((string)args.NewValue);
        }
    }

    private static void OnCommandTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ConfigMakerUserControl control || control._updatingEditor)
        {
            return;
        }

        control._presetDocument.CommandText = (string)args.NewValue;
        control._designerNeedsRefresh = true;
        control.SetEditorText((string)args.NewValue, updateProperty: false);
    }

    private async void ConfigMakerUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateLayoutBounds();
            LoadEditorBackground();
            UpdateZoomStatus();
            RebuildDiagnostics();
            if (!string.IsNullOrWhiteSpace(ComponentId))
            {
                await SetComponentAsync(ComponentId);
            }
            else
            {
                ShowEditorMessage(
                    _localizer.GetLocalizedString("ConfigMakerSelectComponentMessage"),
                    InfoBarSeverity.Informational);
            }
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private void ConfigMakerUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _helpCancellation?.Cancel();
        _diagnosticsTimer.Stop();
        EditorSearchControl.Close();
        _ = StopTestAsync();
    }

    private void ConfigMakerUserControl_ActualThemeChanged(FrameworkElement sender, object args) => ApplyEditorTheme();

    private void ShowEditorMessage(string message, InfoBarSeverity severity)
    {
        string title = severity switch
        {
            InfoBarSeverity.Error => _localizer.GetLocalizedString("SomethingWentWrong"),
            InfoBarSeverity.Warning => _localizer.GetLocalizedString("Attention"),
            _ => _localizer.GetLocalizedString("Information"),
        };
        StatusNotificationRequested?.Invoke(
            this,
            new StatusNotificationRequestedEventArgs(severity, title, message));
        if (!UseInlineStatusMessages)
        {
            EditorInfoBar.IsOpen = false;
            return;
        }

        EditorInfoBar.Title = title;
        EditorInfoBar.Message = message;
        EditorInfoBar.Severity = severity;
        EditorInfoBar.IsOpen = true;
    }

    // An exception must not leave an async void event handler: it would reach the global handler.
    private void ReportHandlerError(Exception exception)
    {
        Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerUserControl), exception.ToString());
        ShowEditorMessage(
            string.Format(_localizer.GetLocalizedString("ConfigMakerActionFailedMessage"), exception.Message),
            InfoBarSeverity.Error);
    }
}
