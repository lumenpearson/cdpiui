using CDPIUI.Shared.Extensions;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.PrettyErrorConversionService;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly StringBuilder outputBuffer = new();
    // Output text as read from the process service, before path replacement.
    private readonly StringBuilder sourceBuffer = new();
    // Finished text of the last run.
    private readonly StringBuilder blockText = new();
    private readonly SolidColorBrush outputBrush = new(Colors.LightGray);
    private readonly ILocalizer localizer = Localizer.Get();
    private ProcessService processManager;
    private int attachmentVersion;
    private bool isLoaded;
    // The last line can still get the rest of a path, so it is replaced again with each chunk.
    private string pendingLine = string.Empty;
    private string pendingDisplay = string.Empty;
    private Run lastRun;
    // Number of the last output change shown (ProcessService numbers each change under its lock).
    private long shownSequence;

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

    public bool IsProcessRunning => processManager?.IsProcessRunning ?? false;
    public string OutputText => outputBuffer.ToString() + pendingDisplay;

    public event Action<bool> RunningStateChanged;

    public async Task RefreshComponentAsync()
    {
        int version = ++attachmentVersion;
        DisconnectHandlers();
        ClearOutput();
        UpdateComponentLabel();

        if (!isLoaded || string.IsNullOrWhiteSpace(ComponentId))
        {
            UpdateRunningState(false, showMessage: false);
            return;
        }

        ProcessService candidate = (await ComponentTasksManager.Instance.GetTaskFromId(ComponentId))?.ProcessManager;
        if (version != attachmentVersion)
        {
            return;
        }

        processManager = candidate;
        if (processManager == null)
        {
            UpdateRunningState(false, showMessage: false);
            return;
        }

        processManager.OutputAppended += ProcessManager_OutputAppended;
        processManager.OutputReset += ProcessManager_OutputReset;
        processManager.ProcessStateChanged += ProcessManager_ProcessStateChanged;
        processManager.ErrorHappens += ProcessManager_ErrorHappens;
        processManager.ProcessNameChanged += ProcessManager_ProcessNameChanged;

        RefreshOutput();
        UpdateRunningState(processManager.IsProcessRunning, showMessage: false);
        await processManager.GetReady(true);
    }

    public void RefreshOutput()
    {
        string text = GetSourceOutput(out shownSequence);
        ClearOutput();
        AppendOutput(text);
    }

    public void ClearOutput()
    {
        outputBuffer.Clear();
        sourceBuffer.Clear();
        blockText.Clear();
        pendingLine = string.Empty;
        pendingDisplay = string.Empty;
        lastRun = null;
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
        isLoaded = true;
        _ = RefreshComponentAsync();
    }

    private void ViewConfigOutput_Unloaded(object sender, RoutedEventArgs e)
    {
        isLoaded = false;
        ++attachmentVersion;
        DisconnectHandlers();
    }

    private void DisconnectHandlers()
    {
        if (processManager == null)
        {
            return;
        }

        processManager.OutputAppended -= ProcessManager_OutputAppended;
        processManager.OutputReset -= ProcessManager_OutputReset;
        processManager.ProcessStateChanged -= ProcessManager_ProcessStateChanged;
        processManager.ErrorHappens -= ProcessManager_ErrorHappens;
        processManager.ProcessNameChanged -= ProcessManager_ProcessNameChanged;
        processManager = null;
    }

    private void ProcessManager_OutputAppended(string output, long sequence) =>
        QueueRefresh(() => AppendReceivedOutput(output, sequence));

    private void ProcessManager_OutputReset() => QueueRefresh(() => SynchronizeOutput());

    private void QueueRefresh(Action action)
    {
        int version = attachmentVersion;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isLoaded && version == attachmentVersion) action();
        });
    }

    private void AppendReceivedOutput(string output, long sequence)
    {
        // The chunk is already part of a snapshot that was shown.
        if (sequence <= shownSequence)
        {
            return;
        }

        // A chunk is missing or came out of order (pipe messages run concurrently): the buffer
        // has the right order.
        if (sequence != shownSequence + 1)
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
        if (sourceBuffer.Length + output.Length > SourceOutputLimit)
        {
            RefreshOutput();
            return;
        }

        shownSequence = sequence;
        AppendOutput(output);
    }

    private void SynchronizeOutput()
    {
        // Read the current snapshot: queued output and full-buffer replies may overlap.
        string text = GetSourceOutput(out shownSequence);
        if (text.Length >= sourceBuffer.Length && sourceBuffer.Equals(text.AsSpan(0, sourceBuffer.Length)))
        {
            AppendOutput(text[sourceBuffer.Length..]);
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

        sourceBuffer.Append(text);
        bool prettyPaths = SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "prettyPathView");

        // Paths never span lines, so finished lines are replaced once and only the last line again.
        string pending = pendingLine + text;
        int lineEnd = pending.AsSpan().LastIndexOfAny('\r', '\n') + 1;
        if (lineEnd == 0 && pending.Length > MaxPendingLineLength)
        {
            lineEnd = pending.Length;
        }

        if (lastRun != null && blockText.Length >= RunBlockLength)
        {
            // The run is full: keep its finished text and continue in a new run.
            lastRun.Text = blockText.ToString();
            lastRun = null;
            blockText.Clear();
        }

        if (lineEnd > 0)
        {
            string finished = PrettifyPaths(pending[..lineEnd], prettyPaths);
            outputBuffer.Append(finished);
            blockText.Append(finished);
        }

        pendingLine = pending[lineEnd..];
        pendingDisplay = PrettifyPaths(pendingLine, prettyPaths);

        if (lastRun == null)
        {
            lastRun = new Run { Foreground = outputBrush };
            OutputParagraph.Inlines.Add(lastRun);
        }
        lastRun.Text = blockText.ToString() + pendingDisplay;
        OutputScrollViewer.ChangeView(null, OutputScrollViewer.ScrollableHeight, null, disableAnimation: true);
    }

    private string GetSourceOutput(out long sequence)
    {
        if (processManager == null)
        {
            sequence = 0;
            return string.Empty;
        }

        return IsCleanOutputMode()
            ? processManager.GetProcessOutput(out sequence)
            : processManager.GetDefaultProcessOutput(out sequence);
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
            ? localizer.GetLocalizedString("NoComponent")
            : string.Format(
                localizer.GetLocalizedString("NowViewOutputFromComponent"),
                item.ShortName.NullIfEmpty() ?? item.Name.NullIfEmpty() ?? item.Id);
    }

    private void UpdateRunningState(bool isRunning, bool showMessage)
    {
        bool hasError = processManager?.IsErrorHappens == true;
        isRunning = isRunning && !hasError;
        StatusIcon.Glyph = isRunning ? "\uEC61" : "\uEB90";
        StatusIcon.Foreground = new SolidColorBrush((Color)Application.Current.Resources[
            isRunning ? "SystemFillColorSuccess" : "SystemFillColorCritical"]);
        StatusTextBlock.Text = localizer.GetLocalizedString(hasError ? "ErrorHappens" : isRunning ? "ProcessStarted" : "ProcessStopped");

        if (hasError)
        {
            StatusInfoBar.Title = localizer.GetLocalizedString("ComponentFailureTitle");
            StatusInfoBar.Message = processManager.LastError?.ErrorCode ?? "UNKNOWN_ERROR";
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.IsOpen = true;
        }
        else if (showMessage)
        {
            string processName = GetProcessName();
            StatusInfoBar.Title = localizer.GetLocalizedString(
                isRunning ? "ProcessStartedMessageTitle" : "ProcessStoppedMessageTitle");
            StatusInfoBar.Message = string.IsNullOrWhiteSpace(processName)
                ? string.Empty
                : string.Format(
                    localizer.GetLocalizedString(
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
        if (!string.IsNullOrWhiteSpace(processManager?.ProcessName))
        {
            return processManager.ProcessName;
        }

        DatabaseStoreItem item = string.IsNullOrWhiteSpace(ComponentId)
            ? null
            : DatabaseHelper.Instance.GetItemById(ComponentId);
        return string.IsNullOrWhiteSpace(item?.Executable) ? string.Empty : $"{item.Executable}.exe";
    }
}
