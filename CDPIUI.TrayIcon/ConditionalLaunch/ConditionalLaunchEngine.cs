using System.Diagnostics;
using System.Windows.Forms;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.ConditionalLaunch;

internal sealed class ConditionalLaunchEngine : IDisposable
{
    private static readonly Lazy<ConditionalLaunchEngine> LazyInstance =
        new(() => new ConditionalLaunchEngine());

    private readonly object _sync = new();
    private readonly ConditionalHotKeyRegistry _hotKeys = new();
    private readonly ProcessTriggerStates _processStates = new();
    private readonly ConditionalTaskRunner _runner;

    private List<ConditionalTask> _tasks = [];
    private List<ConditionalTask> _loadedTasks = [];
    private Control? _messageWindow;
    private IntPtr _windowHandle;
    private string? _tasksDirectory;
    private ConditionalTaskFolderWatcher? _watcher;
    private System.Threading.Timer? _processTimer;
    private System.Threading.Timer? _reloadTimer;
    private int _isPolling;
    // Without process triggers the process list is not read at all.
    private volatile bool _hasProcessTriggers;
    private bool _disposed;

    public static ConditionalLaunchEngine Instance => LazyInstance.Value;

    private ConditionalLaunchEngine()
    {
        _runner = new ConditionalTaskRunner(_sync);
    }

    public void Start(Control messageWindow)
    {
        ArgumentNullException.ThrowIfNull(messageWindow);
        var windowHandle = messageWindow.Handle;
        if (windowHandle == IntPtr.Zero)
            throw new ArgumentException("A valid window handle is required.", nameof(messageWindow));

        lock (_sync)
        {
            if (_windowHandle != IntPtr.Zero)
                return;

            _messageWindow = messageWindow;
            ConfigureTasksDirectory(
                ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
                    SettingsManager.Instance.SettingsFilePath));

            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Starting conditional launch engine. Tasks directory: '{_tasksDirectory}'.");

            _processTimer = new System.Threading.Timer(
                _ => PollProcesses(),
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _windowHandle = windowHandle;
        }

        Reload();
        _processTimer?.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public void Reload(string? tasksDirectory = null)
    {
        var messageWindow = _messageWindow;
        if (messageWindow != null && messageWindow.InvokeRequired)
        {
            try
            {
                messageWindow.BeginInvoke(new Action(() => Reload(tasksDirectory)));
            }
            catch (InvalidOperationException)
            {
                // The window is closing, so the engine stops and needs no reload.
            }
            return;
        }

        string? directory;
        lock (_sync)
        {
            // The directory comes from the UI; the elevated tray only creates and watches one in the
            // application directories.
            if (!string.IsNullOrWhiteSpace(tasksDirectory) &&
                TrustedPaths.IsAllowed(tasksDirectory, "conditional tasks directory"))
                ConfigureTasksDirectory(tasksDirectory);
            directory = _tasksDirectory;
        }

        if (string.IsNullOrWhiteSpace(directory))
            return;

        IReadOnlyList<ConditionalTask> loadedTasks;
        try
        {
            loadedTasks = ConditionalTaskFileService.LoadDirectory(directory, (path, error) =>
                Logger.Instance.CreateWarningLog(
                    nameof(ConditionalLaunchEngine),
                    $"Skipped conditional task file \"{path}\": {error.Message}"));
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(
                nameof(ConditionalLaunchEngine),
                $"Cannot load conditional tasks: {ex}");
            return;
        }

        lock (_sync)
            ApplyLoadedTasks(directory, loadedTasks);
    }

    // The caller holds _sync.
    private void ApplyLoadedTasks(string directory, IReadOnlyList<ConditionalTask> loadedTasks)
    {
        UnregisterHotKeys();

        // Any write to the folder reloads all tasks: only the states of tasks that were
        // removed or changed are reset, so a pending delayed action of another task stays.
        var oldSignatures = _loadedTasks.ToDictionary(task => task.Id, GetSignature, StringComparer.OrdinalIgnoreCase);
        var unchangedIds = loadedTasks
            .Where(task => oldSignatures.TryGetValue(task.Id, out var signature) && signature == GetSignature(task))
            .Select(task => task.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _processStates.ResetExcept(unchangedIds);

        _loadedTasks = loadedTasks.ToList();
        _tasks = _loadedTasks.Where(task => task.IsEnabled).ToList();
        _hasProcessTriggers = EnumerateProcessTriggers().Any(binding =>
            !string.IsNullOrWhiteSpace(binding.Trigger.GetParameter("processName")));
        RegisterHotKeys();

        var fileCount = Directory.EnumerateFiles(
            directory,
            $"*{ConditionalTaskFileService.FileExtension}").Count();
        Logger.Instance.CreateDebugLog(
            nameof(ConditionalLaunchEngine),
            $"Conditional tasks reloaded: {loadedTasks.Count} valid, {_tasks.Count} enabled, {fileCount} files total.");
        if (fileCount != loadedTasks.Count)
        {
            Logger.Instance.CreateWarningLog(
                nameof(ConditionalLaunchEngine),
                $"Ignored {fileCount - loadedTasks.Count} invalid conditional task file(s).");
        }
    }

    public bool HandleWindowMessage(ref Message message)
    {
        if (message.Msg != ConditionalHotKeyRegistry.WmHotKey)
            return false;

        List<ConditionalTask>? tasks;
        lock (_sync)
            tasks = _hotKeys.GetTasks(message.WParam.ToInt32());

        if (tasks == null || tasks.Count == 0)
            return false;

        Logger.Instance.CreateDebugLog(
            nameof(ConditionalLaunchEngine),
            $"Hot key received for {tasks.Count} conditional task(s).");
        _runner.RunHighestPriority(tasks);
        return true;
    }

    public bool RunTask(string? taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return false;

        ConditionalTask? task;
        lock (_sync)
        {
            task = _loadedTasks.FirstOrDefault(item => string.Equals(
                item.Id,
                taskId,
                StringComparison.OrdinalIgnoreCase));
        }

        if (task == null)
        {
            Logger.Instance.CreateWarningLog(
                nameof(ConditionalLaunchEngine),
                $"Cannot run conditional task '{taskId}': task was not found.");
            return false;
        }

        Logger.Instance.CreateDebugLog(
            nameof(ConditionalLaunchEngine),
            $"Manual run requested for conditional task '{task.Name}' ({task.Id}).");
        _ = _runner.RunOnceAsync(task);
        return true;
    }

    // The caller holds _sync.
    private void RegisterHotKeys() => _hotKeys.Register(_windowHandle, _tasks);

    // The caller holds _sync.
    private void UnregisterHotKeys() => _hotKeys.UnregisterAll(_windowHandle);

    private void PollProcesses()
    {
        if (!_hasProcessTriggers)
            return;

        if (Interlocked.Exchange(ref _isPolling, 1) != 0)
            return;

        try
        {
            var runningProcessNames = GetRunningProcessNames();

            List<TriggerBinding> newlyMatched;
            lock (_sync)
                newlyMatched = _processStates.Update(EnumerateProcessTriggers(), runningProcessNames);

            ScheduleHighestPriority(newlyMatched);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(
                nameof(ConditionalLaunchEngine),
                $"Process condition polling failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    private static HashSet<string> GetRunningProcessNames()
    {
        HashSet<string> runningProcessNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                runningProcessNames.Add(process.ProcessName);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // The process exited after the list was taken; it is not running.
            }
            finally
            {
                process.Dispose();
            }
        }
        return runningProcessNames;
    }

    private void ScheduleHighestPriority(IReadOnlyCollection<TriggerBinding> matches)
    {
        if (matches.Count == 0)
            return;

        var taskMatches = matches
            .GroupBy(match => match.Task.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(match => match.Trigger.DelaySeconds).First())
            .ToList();
        var highestPriority = taskMatches.Max(match => (int)match.Task.Priority);
        Logger.Instance.CreateDebugLog(
            nameof(ConditionalLaunchEngine),
            $"Process condition matched {taskMatches.Count} task(s); scheduling priority {highestPriority}.");
        foreach (var match in taskMatches.Where(match => (int)match.Task.Priority == highestPriority))
        {
            CancellationTokenSource? cancellation;
            lock (_sync)
                cancellation = _processStates.BeginPending(match.StateKey);
            if (cancellation == null)
                continue;

            _ = RunAfterDelayAsync(match, cancellation);
        }
    }

    private async Task RunAfterDelayAsync(
        TriggerBinding match,
        CancellationTokenSource cancellation)
    {
        try
        {
            if (match.Trigger.DelaySeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(match.Trigger.DelaySeconds), cancellation.Token);

            if (!cancellation.IsCancellationRequested)
                await _runner.RunOnceAsync(match.Task);
        }
        catch (OperationCanceledException)
        {
            // The trigger was cancelled during the delay; the task must not run.
        }
        finally
        {
            lock (_sync)
                _processStates.EndPending(match.StateKey, cancellation);
            cancellation.Dispose();
        }
    }

    private void ConfigureTasksDirectory(string directory, bool recreateWatcher = false)
    {
        directory = Path.GetFullPath(directory);
        if (!recreateWatcher &&
            string.Equals(_tasksDirectory, directory, StringComparison.OrdinalIgnoreCase) &&
            _watcher != null)
        {
            return;
        }

        _watcher?.Dispose();

        Directory.CreateDirectory(directory);
        _tasksDirectory = directory;
        Logger.Instance.CreateDebugLog(
            nameof(ConditionalLaunchEngine),
            $"Using conditional tasks directory: '{directory}'.");
        _watcher = new ConditionalTaskFolderWatcher(directory, TaskFilesChanged, TaskWatcherError);
    }

    // After a buffer overflow the watcher stops raising events for good: it is created
    // again and the tasks are reloaded, so later changes are not missed.
    private void TaskWatcherError(object sender, ErrorEventArgs e)
    {
        Logger.Instance.CreateWarningLog(nameof(ConditionalLaunchEngine), $"Task folder watcher failed: {e.GetException()?.Message}");
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(_tasksDirectory))
            {
                try
                { ConfigureTasksDirectory(_tasksDirectory, recreateWatcher: true); }
                catch (Exception ex) { Logger.Instance.CreateErrorLog(nameof(ConditionalLaunchEngine), $"Cannot watch the task folder: {ex.Message}"); }
            }
        }
        TaskFilesChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Changed, _tasksDirectory ?? string.Empty, null));
    }

    private static string GetSignature(ConditionalTask task) =>
        System.Text.Json.JsonSerializer.Serialize(task);

    private void TaskFilesChanged(object sender, FileSystemEventArgs e)
    {
        lock (_sync)
        {
            _reloadTimer?.Dispose();
            _reloadTimer = new System.Threading.Timer(
                _ => Reload(),
                null,
                TimeSpan.FromMilliseconds(500),
                Timeout.InfiniteTimeSpan);
        }
    }

    private static bool IsProcessTrigger(ConditionalTriggerType type)
    {
        return type != ConditionalTriggerType.HotKey;
    }

    private IEnumerable<TriggerBinding> EnumerateProcessTriggers()
    {
        foreach (var task in _tasks)
        {
            for (var index = 0; index < task.Triggers.Count; index++)
            {
                var trigger = task.Triggers[index];
                if (IsProcessTrigger(trigger.Type))
                    yield return new TriggerBinding(task, trigger, $"{task.Id}|{index}");
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;

            UnregisterHotKeys();
            _processStates.Clear();
            _loadedTasks.Clear();
            _tasks.Clear();
            _hasProcessTriggers = false;

            _watcher?.Dispose();

            _processTimer?.Dispose();
            _reloadTimer?.Dispose();
            _messageWindow = null;
        }
    }
}
