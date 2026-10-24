using System;
using System.Threading.Tasks;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.ConfigMaker;

/// <summary>
/// The state and the logic of the preset editor: the edited document, the editor text, the
/// status message and the editor background. The other features live in the partial files.
/// </summary>
public sealed partial class ConfigMakerViewModel : ObservableObject
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
    private string _componentId = string.Empty;
    private string _commandText = string.Empty;
    private bool _isEditorReadOnly;
    private bool _updatingComponent;
    private bool _updatingEditor;
    private string _editorBackground = BlackBackground;
    private string _statusTitle = string.Empty;
    private string _statusMessage = string.Empty;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private bool _isStatusOpen;

    public ConfigMakerViewModel(IConfigMakerView view, DispatcherQueue dispatcherQueue)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
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
        CopyCommandCommand = new RelayCommand(CopySelectedCommand, () => HasSelectedCommand);
        InsertCommandCommand = new RelayCommand(InsertSelectedCommand, () => CanInsertCommand);
        CreateVariableCommand = new AsyncRelayCommand(
            () => RunReportingErrorsAsync(CreateVariableAsync),
            logSource: LogSource);
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
            _designerNeedsRefresh = true;
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
            if (!SetProperty(ref _commandText, value) || _updatingEditor)
            {
                return;
            }
            _presetDocument.CommandText = value;
            _designerNeedsRefresh = true;
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
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public InfoBarSeverity StatusSeverity
    {
        get => _statusSeverity;
        private set => SetProperty(ref _statusSeverity, value);
    }

    public bool IsStatusOpen
    {
        get => _isStatusOpen;
        set => SetProperty(ref _isStatusOpen, value);
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
        _helpCancellation?.Cancel();
        _diagnosticsTimer.Stop();
    }

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
    public void OnEditorTextChanged(string text)
    {
        if (_updatingEditor)
        {
            return;
        }
        if (IsEditorReadOnly)
        {
            SetEditorText(CommandText, updateProperty: false);
            return;
        }

        _updatingEditor = true;
        CommandText = text ?? string.Empty;
        _updatingEditor = false;
        _presetDocument.CommandText = CommandText;
        _designerNeedsRefresh = true;
        RebuildPresetStructure();
        CommandTextChanged?.Invoke(CommandText);
        ScheduleDiagnostics();
    }

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

    public void ShowEditorMessage(string message, InfoBarSeverity severity)
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
            IsStatusOpen = false;
            return;
        }

        StatusTitle = title;
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }

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
    {
        _updatingEditor = true;
        string editorText = _view.ReplaceEditorText(text ?? string.Empty);
        if (updateProperty)
        {
            CommandText = editorText;
        }
        _presetDocument.CommandText = editorText;
        _designerNeedsRefresh = true;
        _updatingEditor = false;
        RebuildPresetStructure();
        RebuildDiagnostics();
        CommandTextChanged?.Invoke(editorText);
    }

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
