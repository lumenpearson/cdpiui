using System;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.ConfigMaker;

/// <summary>
/// The state and the logic of the preset editor: the edited document, the editor text, the
/// status message and the editor background. The other features live in the partial files.
/// </summary>
public sealed partial class ConfigMakerViewModel : ObservableObject, IConfigMakerDocumentHost, IConfigMakerPresetFileReplacementHost, IConfigMakerPresetFileActionsHost, IConfigMakerVariableActionsHost
{
    // The log source of the control the view model was extracted from.
    private const string LogSource = "ConfigMakerUserControl";
    private const string BlackBackground = "Black";
    private const string MicaBackground = "Mica";
    private const string MicaTransparentBackground = "MicaTransparent";
    private const string MicaSmokeBackground = "MicaSmoke";

    private readonly IConfigMakerView _view;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly ConfigMakerPresetDocument _presetDocument = new();
    private readonly ConfigMakerPresetResources _presetResources;
    private readonly ConfigMakerDocumentCoordinator _documentCoordinator;
    private readonly ConfigMakerPresetFileReplacer _presetFileReplacer;
    private readonly ConfigMakerPresetFileActions _presetFileActions;
    private readonly ConfigMakerVariableActions _variableActions;
    private readonly TextChangeRelay _textChanges;
    private string _componentId = string.Empty;
    private string _commandText = string.Empty;
    private bool _isEditorReadOnly;
    private bool _updatingComponent;
    private string _editorBackground = BlackBackground;
    private readonly ConfigMakerStatusPresentation<InfoBarSeverity> _statusPresentation;

    public ConfigMakerViewModel(IConfigMakerView view, DispatcherQueue dispatcherQueue)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _statusPresentation = new(InfoBarSeverity.Informational,
            severity => severity switch
            {
                InfoBarSeverity.Error => _localizer.GetLocalizedString("SomethingWentWrong"),
                InfoBarSeverity.Warning => _localizer.GetLocalizedString("Attention"),
                _ => _localizer.GetLocalizedString("Information"),
            },
            (severity, title, message) => StatusNotificationRequested?.Invoke(
                this, new StatusNotificationRequestedEventArgs(severity, title, message)),
            () => UseInlineStatusMessages);
        _statusPresentation.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _textChanges = new TextChangeRelay(
            () => IsEditorReadOnly, () => CommandText, text => CommandText = text,
            _view.ReplaceEditorText,
            text => { _presetDocument.CommandText = text; _simpleDesigner.MarkDirty(); },
            RebuildPresetStructure, RebuildDiagnostics, ScheduleDiagnostics,
            text => CommandTextChanged?.Invoke(text));
        _presetResources = new ConfigMakerPresetResources(
            _presetDocument,
            () => ComponentId,
            () => CommandText);
        _presetFileActions = new ConfigMakerPresetFileActions(_presetDocument.Resources, this,
            new ConfigMakerPresetFileActionsBackend(_presetResources));
        _variableActions = new ConfigMakerVariableActions(_presetDocument, this);
        _presetFileReplacer = new ConfigMakerPresetFileReplacer(this,
            new ConfigMakerPresetFileReplacementBackend(_presetResources));
        _documentCoordinator = new ConfigMakerDocumentCoordinator(
            _presetDocument, this, () => new ConfigMakerPresetStorageService(),
            ConfigMakerPresetExporter.ExportAsync);
        _commandHelpPanel = new ConfigMakerCommandHelpPanel(_localizer.GetLocalizedString, () => IsEditorReadOnly);
        _commandHelpPanel.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _diagnostics = new ConfigMakerDiagnostics<ConfigMakerDiagnosticViewModel>(Diagnostics,
            () => _view.EditorText, () => _commandHelpPanel.Document.Options, _presetDocument.Variables,
            reference => _presetResources.FindAttached(reference) != null, CreateDiagnosticViewModel,
            _localizer.GetLocalizedString, () => _view.RefreshEditorHighlighting());
        _diagnostics.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _helpLoader = new ConfigMakerCommandHelpLoader(_commandHelpPanel, () => ComponentId,
            _localizer.GetLocalizedString, _helpService.LoadAsync,
            RebuildPresetStructure, RebuildDiagnostics, ShowHelpLoadStatus);
        _helpLoader.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _testSession = new ConfigMakerTestSession(
            new ConfigMakerComponentTestTasks(),
            () => ComponentId,
            ConfirmReplaceRunningComponentAsync,
            id => DatabaseHelper.Instance.IsItemInstalled(id),
            ShowTestNotice,
            _view.RefreshOutputAsync,
            action => _dispatcherQueue.TryEnqueue(async () => await action()));
        _testSession.StateChanged += (_, _) => RaiseTestStateChanged();
        _zapret2OptionDescriber = new Zapret2PresetOptionDescriber(_localizer);
        _presetTreeBuilder = new ConfigMakerPresetTreeBuilder(
            _localizer.GetLocalizedString,
            _presetResources.TryResolvePath,
            System.IO.File.Exists,
            option => _zapret2OptionDescriber.Describe(option, _commandHelpPanel.Document.Options));
        _presetStructure = new ConfigMakerPresetStructure<ConfigMakerTreeNode>(
            () => CommandText, () => IsZapret2Component,
            _presetResources.ExtractFiles, _presetResources.GetAttachedFiles,
            files => ConfigMakerPresetTreeAdapter.ToFileNodes(_presetTreeBuilder.BuildFiles(files)),
            (options, groups, isZapret2) => ConfigMakerPresetTreeAdapter.ToOptionNodes(
                _presetTreeBuilder.BuildGroups(options, groups, isZapret2)));
        _presetStructure.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _presetStructure.AvailabilityChanged += (_, _) => PresetStructureAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        DesignerTextValueChangedCommand = new RelayCommand(parameter =>
            _simpleDesigner.HandleDesignerTextValueChanged((Tuple<string, string>)parameter));
        DesignerBoolValueToggledCommand = new RelayCommand(parameter =>
            _simpleDesigner.HandleDesignerBoolValueToggled((Tuple<string, bool>)parameter));
        DesignerSelectedGuidChangedCommand = new RelayCommand(parameter =>
            _simpleDesigner.HandleDesignerSelectedGuidChanged((Tuple<string, string>)parameter));
        _simpleDesigner = new ConfigMakerSimpleDesigner(
            new ConfigMakerSimpleDesignerBackend(),
            () => ComponentId,
            () => _view.EditorText,
            text => SetEditorText(text),
            new(DesignerTextValueChangedCommand, DesignerBoolValueToggledCommand, DesignerSelectedGuidChangedCommand),
            ShowDesignerNotice);
        _simpleDesigner.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        CopyCommandCommand = new RelayCommand(CopySelectedCommand, () => HasSelectedCommand);
        InsertCommandCommand = new RelayCommand(InsertSelectedCommand, () => CanInsertCommand);
        CreateVariableCommand = new AsyncRelayCommand(() => RunReportingErrorsAsync(CreateVariableAsync));
        AddPresetFileCommand = new RelayCommand(AddPresetFilesFromPicker);
        SelectEditorBackgroundCommand = new RelayCommand(parameter => SelectEditorBackground(parameter as string));

        _diagnosticsTimer = dispatcherQueue.CreateTimer();
        _diagnosticsTimer.Interval = TimeSpan.FromMilliseconds(300);
        _diagnosticsTimer.IsRepeating = false;
        _diagnosticsTimer.Tick += DiagnosticsTimer_Tick;
        _presetDocument.ContentChanged += PresetDocument_ContentChanged;
    }

    public event Action<string> CommandTextChanged;
    public event EventHandler<StatusNotificationRequestedEventArgs> StatusNotificationRequested;

    public RelayCommand SelectEditorBackgroundCommand { get; }

    public ConfigMakerPresetDocument PresetDocument => _presetDocument;

    /// <summary>
    /// The component of the edited command. A change outside <see cref="SetComponentAsync"/> loads
    /// the component once the view is loaded.
    /// </summary>
    public string ComponentId
    {
        get => _componentId;
        set
        {
            if (!SetProperty(ref _componentId, value))
            {
                return;
            }
            _presetDocument.ComponentId = value;
            _simpleDesigner.MarkDirty();
            UpdateSimpleDesignerAvailability();
            if (!_updatingComponent && _view.IsViewLoaded)
            {
                _ = SetComponentAsync(value);
            }
        }
    }

    /// <summary>The edited command. A change from outside the editor replaces the editor text.</summary>
    public string CommandText
    {
        get => _commandText;
        set
        {
            if (!SetProperty(ref _commandText, value) || _textChanges.IsUpdating)
            {
                return;
            }
            _presetDocument.CommandText = value;
            _simpleDesigner.MarkDirty();
            SetEditorText(value, updateProperty: false);
        }
    }

    public bool IsEditorReadOnly
    {
        get => _isEditorReadOnly;
        set
        {
            if (SetProperty(ref _isEditorReadOnly, value))
            {
                OnPropertiesChanged(nameof(IsEditorWritable), nameof(CanInsertCommand));
                InsertCommandCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEditorWritable => !_isEditorReadOnly;

    public bool UseInlineStatusMessages { get; set; } = true;

    public string StatusTitle
    {
        get => _statusPresentation.StatusTitle;
        private set => _statusPresentation.StatusTitle = value;
    }

    public string StatusMessage
    {
        get => _statusPresentation.StatusMessage;
        private set => _statusPresentation.StatusMessage = value;
    }

    public InfoBarSeverity StatusSeverity
    {
        get => _statusPresentation.StatusSeverity;
        private set => _statusPresentation.StatusSeverity = value;
    }

    public bool IsStatusOpen
    {
        get => _statusPresentation.IsStatusOpen;
        set => _statusPresentation.IsStatusOpen = value;
    }

    public string EditorBackground => _editorBackground;
    public bool IsBlackEditorBackground => _editorBackground == BlackBackground;
    public bool IsMicaEditorBackground => _editorBackground == MicaBackground;
    public bool IsMicaTransparentEditorBackground => _editorBackground == MicaTransparentBackground;
    public bool IsMicaSmokeEditorBackground => _editorBackground == MicaSmokeBackground;

    public async Task OnViewLoadedAsync()
    {
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

    public void CancelBackgroundWork()
    {
        _helpLoader.Cancel();
        _diagnosticsTimer.Stop();
    }

    public async Task SetComponentAsync(string componentId)
    {
        componentId = componentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return;
        }

        if (_testSession.IsTesting && !string.Equals(_testSession.TestComponentId, componentId, StringComparison.OrdinalIgnoreCase))
        {
            await StopTestAsync();
        }

        _updatingComponent = true;
        ComponentId = componentId;
        _updatingComponent = false;
        _presetDocument.ComponentId = componentId;
        _view.SetOutputComponent(componentId);
        UpdateSimpleDesignerAvailability();
        RebuildPresetStructure();
        await LoadHelpAsync(componentId, forceRefresh: false);
    }

    public void FormatCommand()
    {
        if (IsEditorReadOnly)
        {
            return;
        }
        SetEditorText(ComponentCommandLineFormatter.FormatByFlags(_view.EditorText));
        _view.SetCursorPosition(0, 0, focus: false);
    }

    /// <summary>Called by the view when the user changed the editor text.</summary>
    public void OnEditorTextChanged(string text) => _textChanges.OnTextChanged(text);

    public void LoadEditorBackground()
    {
        string savedBackground = SettingsManager.Instance.GetValue<string>(
            "APPEARANCE",
            "configEditorBackground");
        _editorBackground = savedBackground switch
        {
            MicaBackground => MicaBackground,
            MicaTransparentBackground => MicaTransparentBackground,
            MicaSmokeBackground => MicaSmokeBackground,
            _ => BlackBackground,
        };
        RaiseEditorBackgroundChanged();
    }

    public void ShowEditorMessage(string message, InfoBarSeverity severity) =>
        _statusPresentation.Show(message, severity);

    // An exception must not leave an async void event handler: it would reach the global handler.
    public void ReportHandlerError(Exception exception)
    {
        Core.Basic.Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        ShowEditorMessage(
            string.Format(_localizer.GetLocalizedString("ConfigMakerActionFailedMessage"), exception.Message),
            InfoBarSeverity.Error);
    }

    private async Task RunReportingErrorsAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private void SetEditorText(string text, bool updateProperty = true)
        => _textChanges.ReplaceText(text, updateProperty);

    private void InsertAtCursor(string command)
    {
        (int cursorLine, int cursorColumn) = _view.GetCursorPosition();
        string result = ConfigMakerCommandText.InsertOnOwnLine(
            _view.EditorText,
            cursorLine,
            cursorColumn,
            command,
            out int newCursorIndex);

        SetEditorText(result.Replace("\n", Environment.NewLine));
        (int line, int column) = ConfigMakerCommandText.GetLineAndColumn(result, newCursorIndex);
        _view.SetCursorPosition(line, column, focus: true);
    }

    private void SelectEditorBackground(string background)
    {
        _editorBackground = background switch
        {
            MicaBackground => MicaBackground,
            MicaTransparentBackground => MicaTransparentBackground,
            MicaSmokeBackground => MicaSmokeBackground,
            _ => BlackBackground,
        };
        SettingsManager.Instance.SetValue(
            "APPEARANCE",
            "configEditorBackground",
            _editorBackground);
        RaiseEditorBackgroundChanged();
    }

    // Raised even when the value is the same: the view applies the brush on every load.
    private void RaiseEditorBackgroundChanged() => OnPropertiesChanged(
        nameof(IsBlackEditorBackground),
        nameof(IsMicaEditorBackground),
        nameof(IsMicaTransparentEditorBackground),
        nameof(IsMicaSmokeEditorBackground),
        nameof(EditorBackground));
}
