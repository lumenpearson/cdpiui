using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.PrettyErrorConversionService;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinUI3Localizer;

namespace CDPIUI.Controls.Universal;

public sealed partial class ViewConfigOutput : UserControl
{
    public static readonly DependencyProperty ComponentIdProperty = DependencyProperty.Register(
        nameof(ComponentId),
        typeof(string),
        typeof(ViewConfigOutput),
        new PropertyMetadata(string.Empty, OnComponentIdChanged));

    // The tray keeps this many characters of output and then drops the older half.
    private const int SourceOutputLimit = 2 * 1024 * 1024;
    // Shown text is kept in runs of about this size instead of one run per chunk.
    private const int RunBlockLength = 16 * 1024;
    // A line longer than this is shown as finished without waiting for its end.
    private const int MaxPendingLineLength = 64 * 1024;

    // Shown text of finished lines.
    private readonly StringBuilder _outputBuffer = new();
    // Output text as read from the process service, before path replacement.
    private readonly StringBuilder _sourceBuffer = new();
    // Finished text of the last run.
    private readonly StringBuilder _blockText = new();
    private readonly SolidColorBrush _outputBrush = new(Colors.LightGray);
    private readonly ILocalizer _localizer = Localizer.Get();
    private ProcessService _processManager;
    private int _attachmentVersion;
    private bool _isLoaded;
    // The last line can still get the rest of a path, so it is replaced again with each chunk.
    private string _pendingLine = string.Empty;
    private string _pendingDisplay = string.Empty;
    private Run _lastRun;
    // Number of the last output change shown (ProcessService numbers each change under its lock).
    private long _shownSequence;

    public ViewConfigOutput()
    {
        InitializeComponent();
        Loaded += ViewConfigOutput_Loaded;
        Unloaded += ViewConfigOutput_Unloaded;

        UpdateComponentLabel();
        UpdateRunningState(false, showMessage: false);
    }

    public string ComponentId
    {
        get => (string)GetValue(ComponentIdProperty);
        set => SetValue(ComponentIdProperty, value);
    }

    public bool IsProcessRunning => _processManager?.IsProcessRunning ?? false;
    public string OutputText => _outputBuffer.ToString() + _pendingDisplay;

    public event Action<bool> RunningStateChanged;

    public async Task RefreshComponentAsync()
    {
        int version = ++_attachmentVersion;
        DisconnectHandlers();
        ClearOutput();
        UpdateComponentLabel();

        if (!_isLoaded || string.IsNullOrWhiteSpace(ComponentId))
        {
            UpdateRunningState(false, showMessage: false);
            return;
        }

        ProcessService candidate = (await ComponentTasksManager.Instance.GetTaskFromId(ComponentId))?.ProcessManager;
        if (version != _attachmentVersion)
        {
            return;
        }

        _processManager = candidate;
        if (_processManager == null)
        {
            UpdateRunningState(false, showMessage: false);
            return;
        }

        _processManager.OutputAppended += ProcessManager_OutputAppended;
        _processManager.OutputReset += ProcessManager_OutputReset;
        _processManager.ProcessStateChanged += ProcessManager_ProcessStateChanged;
        _processManager.ErrorHappens += ProcessManager_ErrorHappens;
        _processManager.ProcessNameChanged += ProcessManager_ProcessNameChanged;

        RefreshOutput();
        UpdateRunningState(_processManager.IsProcessRunning, showMessage: false);
        await _processManager.GetReady(true);
    }

    public void RefreshOutput()
    {
        string text = GetSourceOutput(out _shownSequence);
        ClearOutput();
        AppendOutput(text);
    }

    public void ClearOutput()
    {
        _outputBuffer.Clear();
        _sourceBuffer.Clear();
        _blockText.Clear();
        _pendingLine = string.Empty;
        _pendingDisplay = string.Empty;
        _lastRun = null;
        OutputParagraph.Inlines.Clear();
    }

    public void CopyAll()
    {
        DataPackage package = new();
        package.SetText(OutputText);
        Clipboard.SetContent(package);
    }

    private static void OnComponentIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ViewConfigOutput control)
        {
            _ = control.RefreshComponentAsync();
        }
    }

    private void ViewConfigOutput_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _ = RefreshComponentAsync();
    }

    private void ViewConfigOutput_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        ++_attachmentVersion;
        DisconnectHandlers();
    }

    private void DisconnectHandlers()
    {
        if (_processManager == null)
        {
            return;
        }

        _processManager.OutputAppended -= ProcessManager_OutputAppended;
        _processManager.OutputReset -= ProcessManager_OutputReset;
        _processManager.ProcessStateChanged -= ProcessManager_ProcessStateChanged;
        _processManager.ErrorHappens -= ProcessManager_ErrorHappens;
        _processManager.ProcessNameChanged -= ProcessManager_ProcessNameChanged;
        _processManager = null;
    }

    private void ProcessManager_OutputAppended(string output, long sequence) =>
        QueueRefresh(() => AppendReceivedOutput(output, sequence));

    private void ProcessManager_OutputReset() => QueueRefresh(() => SynchronizeOutput());

    private void QueueRefresh(Action action)
    {
        int version = _attachmentVersion;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isLoaded && version == _attachmentVersion)
                action();
        });
    }

    private void AppendReceivedOutput(string output, long sequence)
    {
        // The chunk is already part of a snapshot that was shown.
        if (sequence <= _shownSequence)
        {
            return;
        }

        // A chunk is missing or came out of order (pipe messages run concurrently): the buffer
        // has the right order.
        if (sequence != _shownSequence + 1)
        {
            SynchronizeOutput();
            return;
        }

        // The event carries the cleaned text only, so the raw view is compared with the buffer.
        if (!IsCleanOutputMode())
        {
            SynchronizeOutput();
            return;
        }

        // The tray dropped the older half of its buffer: show what it keeps now.
        if (_sourceBuffer.Length + output.Length > SourceOutputLimit)
        {
            RefreshOutput();
            return;
        }

        _shownSequence = sequence;
        AppendOutput(output);
    }

    private void SynchronizeOutput()
    {
        // Read the current snapshot: queued output and full-buffer replies may overlap.
        string text = GetSourceOutput(out _shownSequence);
        if (text.Length >= _sourceBuffer.Length && _sourceBuffer.Equals(text.AsSpan(0, _sourceBuffer.Length)))
        {
            AppendOutput(text[_sourceBuffer.Length..]);
        }
        else
        {
            ClearOutput();
            AppendOutput(text);
        }
    }

    private void ProcessManager_ProcessStateChanged(Tuple<string, bool> state)
    {
        // ComponentId is a DependencyProperty and may be read on the UI thread only.
        QueueRefresh(() =>
        {
            if (!string.Equals(state.Item1, ComponentId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            SynchronizeOutput();
            UpdateRunningState(IsProcessRunning, showMessage: true);
        });
    }

    private void ProcessManager_ProcessNameChanged(string processName)
    {
        QueueRefresh(() =>
        {
            UpdateComponentLabel();
            UpdateRunningState(IsProcessRunning, showMessage: true);
        });
    }

    private void ProcessManager_ErrorHappens(ErrorModel error)
    {
        QueueRefresh(() => UpdateRunningState(IsProcessRunning, showMessage: true));
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _sourceBuffer.Append(text);
        bool prettyPaths = SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "prettyPathView");

        // Paths never span lines, so finished lines are replaced once and only the last line again.
        string pending = _pendingLine + text;
        int lineEnd = pending.AsSpan().LastIndexOfAny('\r', '\n') + 1;
        if (lineEnd == 0 && pending.Length > MaxPendingLineLength)
        {
            lineEnd = pending.Length;
        }

        if (_lastRun != null && _blockText.Length >= RunBlockLength)
        {
            // The run is full: keep its finished text and continue in a new run.
            _lastRun.Text = _blockText.ToString();
            _lastRun = null;
            _blockText.Clear();
        }

        if (lineEnd > 0)
        {
            string finished = PrettifyPaths(pending[..lineEnd], prettyPaths);
            _outputBuffer.Append(finished);
            _blockText.Append(finished);
        }

        _pendingLine = pending[lineEnd..];
        _pendingDisplay = PrettifyPaths(_pendingLine, prettyPaths);

        if (_lastRun == null)
        {
            _lastRun = new Run { Foreground = _outputBrush };
            OutputParagraph.Inlines.Add(_lastRun);
        }
        _lastRun.Text = _blockText.ToString() + _pendingDisplay;
        OutputScrollViewer.ChangeView(null, OutputScrollViewer.ScrollableHeight, null, disableAnimation: true);
    }

    private string GetSourceOutput(out long sequence)
    {
        if (_processManager == null)
        {
            sequence = 0;
            return string.Empty;
        }

        return IsCleanOutputMode()
            ? _processManager.GetProcessOutput(out sequence)
            : _processManager.GetDefaultProcessOutput(out sequence);
    }

    private static bool IsCleanOutputMode() => SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "outputMode");

    private static string PrettifyPaths(string text, bool prettyPaths) =>
        prettyPaths && text.Length > 0 ? DirectoryReplaceRegex().Replace(text, "cdpi-ui://") : text;

    // Same as the path pattern of the process service, but a match stops at a line break.
    [GeneratedRegex(@"(?:[a-zA-Z]):\\[^/\r\n]*?/")]
    private static partial Regex DirectoryReplaceRegex();

    private void UpdateComponentLabel()
    {
        DatabaseStoreItem item = string.IsNullOrWhiteSpace(ComponentId)
            ? null
            : DatabaseHelper.Instance.GetItemById(ComponentId);
        SelectedComponentTextBlock.Text = item == null
            ? _localizer.GetLocalizedString("NoComponent")
            : string.Format(
                _localizer.GetLocalizedString("NowViewOutputFromComponent"),
                item.ShortName.NullIfEmpty() ?? item.Name.NullIfEmpty() ?? item.Id);
    }

    private void UpdateRunningState(bool isRunning, bool showMessage)
    {
        bool hasError = _processManager?.IsErrorHappens == true;
        isRunning = isRunning && !hasError;
        StatusIcon.Glyph = isRunning ? "\uEC61" : "\uEB90";
        StatusIcon.Foreground = new SolidColorBrush((Color)Application.Current.Resources[
            isRunning ? "SystemFillColorSuccess" : "SystemFillColorCritical"]);
        StatusTextBlock.Text = _localizer.GetLocalizedString(hasError ? "ErrorHappens" : isRunning ? "ProcessStarted" : "ProcessStopped");

        if (hasError)
        {
            StatusInfoBar.Title = _localizer.GetLocalizedString("ComponentFailureTitle");
            StatusInfoBar.Message = _processManager.LastError?.ErrorCode ?? "UNKNOWN_ERROR";
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.IsOpen = true;
        }
        else if (showMessage)
        {
            string processName = GetProcessName();
            StatusInfoBar.Title = _localizer.GetLocalizedString(
                isRunning ? "ProcessStartedMessageTitle" : "ProcessStoppedMessageTitle");
            StatusInfoBar.Message = string.IsNullOrWhiteSpace(processName)
                ? string.Empty
                : string.Format(
                    _localizer.GetLocalizedString(
                        isRunning ? "ProcessStartedMessageMessage" : "ProcessStoppedMessageMessage"),
                    processName);
            StatusInfoBar.Severity = isRunning
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Informational;
            StatusInfoBar.IsOpen = true;
        }
        else
        {
            StatusInfoBar.IsOpen = false;
        }

        RunningStateChanged?.Invoke(isRunning);
    }

    private string GetProcessName()
    {
        if (!string.IsNullOrWhiteSpace(_processManager?.ProcessName))
        {
            return _processManager.ProcessName;
        }

        DatabaseStoreItem item = string.IsNullOrWhiteSpace(ComponentId)
            ? null
            : DatabaseHelper.Instance.GetItemById(ComponentId);
        return string.IsNullOrWhiteSpace(item?.Executable) ? string.Empty : $"{item.Executable}.exe";
    }
}
