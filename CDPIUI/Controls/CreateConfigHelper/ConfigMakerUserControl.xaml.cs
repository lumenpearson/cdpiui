using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.Controls.CreateConfigHelper;

/// <summary>
/// The preset editor. The state and the logic live in <see cref="ConfigMakerViewModel"/>; the
/// control keeps the editor, the panels, the trees, the dialogs and the public members other
/// views use.
/// </summary>
public sealed partial class ConfigMakerUserControl : UserControl, IConfigMakerView
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
        new PropertyMetadata(true));

    private readonly ILocalizer _localizer = Localizer.Get();

    public ConfigMakerUserControl()
    {
        ViewModel = new ConfigMakerViewModel(this, DispatcherQueue);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.CommandTextChanged += text => CommandTextChanged?.Invoke(text);
        ViewModel.TestStateChanged += (_, _) => TestStateChanged?.Invoke(this, EventArgs.Empty);
        ViewModel.PresetFileReplaced += (_, args) => PresetFileReplaced?.Invoke(this, args);
        ViewModel.StatusNotificationRequested += (_, args) => StatusNotificationRequested?.Invoke(this, args);
        ViewModel.DocumentStateChanged += (_, _) => DocumentStateChanged?.Invoke(this, EventArgs.Empty);
        ViewModel.PresetStructureAvailabilityChanged += (_, _) => PanelStateChanged?.Invoke(this, EventArgs.Empty);
        InitializeComponent();

        ZoomIn = new RelayCommand(p => ChangeZoom(5));
        ZoomOut = new RelayCommand(p => ChangeZoom(-5));
        ZoomReset = new RelayCommand(p => ChangeZoom(0));

        Search = new RelayCommand(p => EditorSearchControl.ShowSearch(CommandEditor));
        Replace = new RelayCommand(p => EditorSearchControl.ShowReplace(CommandEditor));

        CommandEditor.UseSpacesInsteadTabs = true;
        CommandEditor.NumberOfSpacesForTab = 4;
        ApplyEditorTheme();

        Loaded += ConfigMakerUserControl_Loaded;
        Unloaded += ConfigMakerUserControl_Unloaded;
        ActualThemeChanged += ConfigMakerUserControl_ActualThemeChanged;
    }

    public event Action<string> CommandTextChanged;
    public event EventHandler TestStateChanged;
    public event EventHandler EditorReadOnlyChanged;
    public event EventHandler PanelStateChanged;
    public event EventHandler<ConfigMakerPresetFileReplacedEventArgs> PresetFileReplaced;
    public event EventHandler<StatusNotificationRequestedEventArgs> StatusNotificationRequested;
    public event EventHandler DocumentStateChanged;

    public ConfigMakerViewModel ViewModel { get; }

    public ICommand ZoomIn { get; }
    public ICommand ZoomOut { get; }
    public ICommand ZoomReset { get; }
    public ICommand Search { get; }
    public ICommand Replace { get; }
    public ConfigMakerPresetDocument PresetDocument => ViewModel.PresetDocument;

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

    public bool UseInlineStatusMessages
    {
        get => ViewModel.UseInlineStatusMessages;
        set => ViewModel.UseInlineStatusMessages = value;
    }

    public bool IsTesting => ViewModel.IsTesting;
    public bool IsPresetStructureVisible => ViewModel.IsPresetStructureVisible;
    public bool HasPresetFiles => ViewModel.HasPresetFiles;
    public bool HasPresetGroups => ViewModel.HasPresetGroups;
    public bool HasVariables => ViewModel.HasVariables;
    public bool CanExportText => ViewModel.CanExportText;
    public bool IsSimpleDesignerSupported => ViewModel.IsSimpleDesignerSupported;

    bool IConfigMakerView.IsViewLoaded => IsLoaded;

    public Task SetComponentAsync(string componentId) => ViewModel.SetComponentAsync(componentId);
    public Task RefreshHelpAsync() => ViewModel.RefreshHelpAsync();
    public void LoadPresetDocument(ConfigMakerPresetDocument document) => ViewModel.LoadPresetDocument(document);

    public Task LoadConfigItem(ConfigItem item, bool applyAutoCorrectorSilently = false) =>
        ViewModel.LoadConfigItem(item, applyAutoCorrectorSilently);

    public ConfigItem CreateConfigItem(string packId, string presetName) =>
        ViewModel.CreateConfigItem(packId, presetName);

    public Task<ConfigMakerPresetSaveResult> SaveToApplicationAsync() => ViewModel.SaveToApplicationAsync();
    public Task NewDocumentAsync() => ViewModel.NewDocumentAsync();
    public Task SaveTextAsync() => ViewModel.SaveTextAsync();
    public void FormatCommand() => ViewModel.FormatCommand();
    public Task StartTestAsync() => ViewModel.StartTestAsync();
    public Task StopTestAsync(bool showCompletionMessage = false) => ViewModel.StopTestAsync(showCompletionMessage);
    public void AddPresetFiles(IEnumerable<string> filePaths) => ViewModel.AddPresetFiles(filePaths);
    public void ClearPresetStructure() => ViewModel.ClearPresetStructure();

    public void SetPresetStructure(
        IEnumerable<ConfigMakerPresetFileInfo> files,
        IEnumerable<ConfigMakerPresetGroupInfo> groups)
    {
        ViewModel.SetPresetStructure(files, groups);
        SetPresetFilesPanelVisible(ViewModel.HasPresetFiles);
    }

    void IConfigMakerView.SetOutputComponent(string componentId) => ConfigOutput.ComponentId = componentId;

    Task IConfigMakerView.RefreshOutputAsync() => ConfigOutput.RefreshComponentAsync();

    private static void OnIsEditorReadOnlyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        ConfigMakerUserControl control = (ConfigMakerUserControl)dependencyObject;
        control.ViewModel.IsEditorReadOnly = (bool)args.NewValue;
        control.EditorReadOnlyChanged?.Invoke(control, EventArgs.Empty);
    }

    private static void OnComponentIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ConfigMakerUserControl control)
        {
            control.ViewModel.ComponentId = (string)args.NewValue;
        }
    }

    private static void OnCommandTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ConfigMakerUserControl control)
        {
            control.ViewModel.CommandText = (string)args.NewValue;
        }
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConfigMakerViewModel.ComponentId):
                ComponentId = ViewModel.ComponentId;
                break;
            case nameof(ConfigMakerViewModel.CommandText):
                CommandText = ViewModel.CommandText;
                break;
            case nameof(ConfigMakerViewModel.IsSimpleDesignerSupported):
                UpdateSimpleDesignerTab();
                break;
            case nameof(ConfigMakerViewModel.PresetFileNodes):
                FillTree(PresetFilesTreeView, ViewModel.PresetFileNodes);
                break;
            case nameof(ConfigMakerViewModel.PresetGroupNodes):
                FillTree(PresetGroupsTreeView, ViewModel.PresetGroupNodes);
                UpdatePresetGroupsTab();
                break;
            case nameof(ConfigMakerViewModel.EditorBackground):
                ApplyEditorBackground();
                break;
        }
    }

    private async void ConfigMakerUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateLayoutBounds();
            ViewModel.LoadEditorBackground();
            UpdateZoomStatus();
            await ViewModel.OnViewLoadedAsync();
        }
        catch (Exception exception)
        {
            ViewModel.ReportHandlerError(exception);
        }
    }

    private void ConfigMakerUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelBackgroundWork();
        EditorSearchControl.Close();
        _ = ViewModel.StopTestAsync();
    }

    private void ConfigMakerUserControl_ActualThemeChanged(FrameworkElement sender, object args) => ApplyEditorTheme();
}
