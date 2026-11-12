#nullable enable

using System;
using System.Threading.Tasks;

namespace CDPIUI.Helper.CreateConfigHelper;

internal enum ConfigMakerTestNotice
{
    Started,
    Stopped,
    Exited,
    NotInstalled,
    StartFailed,
}

// Owns one editor test run. The host validates/resolves arguments and supplies UI effects.
internal sealed class ConfigMakerTestSession
{
    private readonly IConfigMakerTestTasks _tasks;
    private readonly Func<string> _currentComponent;
    private readonly Func<Task<bool>> _confirmReplacement;
    private readonly Func<string, bool> _isInstalled;
    private readonly Action<ConfigMakerTestNotice, string?> _notice;
    private readonly Func<Task> _refreshOutput;
    private readonly Action<Func<Task>> _enqueue;
    private bool _isTesting;
    private bool _isStartingTest;
    private bool _restoreComponentAfterTest;
    private string _testComponentId = string.Empty;

    public ConfigMakerTestSession(
        IConfigMakerTestTasks tasks,
        Func<string> currentComponent,
        Func<Task<bool>> confirmReplacement,
        Func<string, bool> isInstalled,
        Action<ConfigMakerTestNotice, string?> notice,
        Func<Task> refreshOutput,
        Action<Func<Task>> enqueue)
    {
        _tasks = tasks;
        _currentComponent = currentComponent;
        _confirmReplacement = confirmReplacement;
        _isInstalled = isInstalled;
        _notice = notice;
        _refreshOutput = refreshOutput;
        _enqueue = enqueue;
    }

    public event EventHandler? StateChanged;
    public bool IsTesting => _isTesting;
    public string TestComponentId => _testComponentId;

    public async Task StartAsync(string arguments)
    {
        await StopAsync();
        bool componentWasRunning = await _tasks.IsRunningAsync(_currentComponent());
        if (componentWasRunning && !await _confirmReplacement())
        {
            return;
        }

        _restoreComponentAfterTest = componentWasRunning;
        await _tasks.StopAsync(_currentComponent());

        _testComponentId = _currentComponent();
        _isTesting = true;
        _isStartingTest = true;
        _tasks.StateUpdated += TaskStateUpdated;
        RaiseStateChanged();

        if (!_isInstalled(_testComponentId))
        {
            _notice(ConfigMakerTestNotice.NotInstalled, null);
            await StopAsync();
            return;
        }

        _notice(ConfigMakerTestNotice.Started, null);
        try
        {
            await _tasks.RunAsync(_testComponentId, arguments);
            _isStartingTest = false;
            await _refreshOutput();
        }
        catch (Exception exception)
        {
            _isStartingTest = false;
            _notice(ConfigMakerTestNotice.StartFailed, exception.Message);
            await StopAsync();
        }
    }

    public async Task StopAsync(bool showCompletionMessage = false)
    {
        if (!_isTesting)
        {
            return;
        }

        string componentId = _testComponentId;
        bool restore = _restoreComponentAfterTest;
        _isTesting = false;
        _isStartingTest = false;
        _tasks.StateUpdated -= TaskStateUpdated;
        RaiseStateChanged();

        if (!string.IsNullOrWhiteSpace(componentId))
        {
            await _tasks.StopAsync(componentId);
            if (restore)
            {
                await _tasks.RestoreAsync(componentId);
            }
        }

        _testComponentId = string.Empty;
        _restoreComponentAfterTest = false;
        if (showCompletionMessage)
        {
            _notice(ConfigMakerTestNotice.Stopped, null);
        }
    }

    private void TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!_isTesting || _isStartingTest ||
            !string.Equals(state.Item1, _testComponentId, StringComparison.OrdinalIgnoreCase) || state.Item2)
        {
            return;
        }
        _enqueue(async () =>
        {
            _notice(ConfigMakerTestNotice.Exited, null);
            await StopAsync();
        });
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
