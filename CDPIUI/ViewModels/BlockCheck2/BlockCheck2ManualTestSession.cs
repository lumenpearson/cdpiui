using System;
using System.Threading.Tasks;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>Owns the result page's manual component test and its original restore policy.</summary>
internal sealed class BlockCheck2ManualTestSession
{
    private readonly IBlockCheck2ManualTestHost _host;
    private readonly Func<IConfigMakerTestTasks> _tasks;
    private readonly Func<string> _componentId;
    private readonly Action<Tuple<string, bool>> _stateHandler;
    private bool _isStartingTest;
    private bool _isRunning;
    private bool _restoreComponentAfterTest;

    public BlockCheck2ManualTestSession(IBlockCheck2ManualTestHost host, Func<IConfigMakerTestTasks> tasks,
        Func<string> componentId, Action<Tuple<string, bool>> stateHandler)
    {
        _host = host;
        _tasks = tasks;
        _componentId = componentId;
        _stateHandler = stateHandler;
    }

    public bool IsRunning => _isRunning;

    public async Task ToggleFromButtonAsync()
    {
        if (_host.IsManualTestChecked == true)
        {
            await StartAsync();
        }
        else
        {
            await StopAsync(true);
        }
    }

    public async Task ToggleAsync()
    {
        if (_isRunning)
        {
            await StopAsync(showCompletionMessage: true);
        }
        else
        {
            await StartAsync();
        }
    }

    public async Task StopAsync(bool showCompletionMessage = false)
    {
        _host.UpdateSelectedStrategy();
        if (!_isRunning)
        {
            return;
        }

        bool restore = _restoreComponentAfterTest;
        _isRunning = false;
        _isStartingTest = false;
        _tasks().StateUpdated -= _stateHandler;

        if (!string.IsNullOrWhiteSpace(_componentId()))
        {
            await _tasks().StopAsync(_componentId());
            if (restore)
            {
                await _tasks().RestoreAsync(_componentId());
            }
        }

        _restoreComponentAfterTest = false;
        if (showCompletionMessage)
        {
            _host.ShowMessage(
                BlockCheck2NoticeSeverity.Informational,
                _host.Text("ManualTesting"),
                _host.Text("ConfigMakerTestStoppedMessage"));
        }

        _host.UpdateSelectedStrategy();
        _host.UpdateAssignmentView();
    }

    public async Task StopOnLeaveAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        bool restore = _restoreComponentAfterTest;
        _isRunning = false;
        _isStartingTest = false;
        _restoreComponentAfterTest = false;
        _tasks().StateUpdated -= _stateHandler;

        string componentId = _componentId();
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return;
        }
        try
        {
            await _tasks().StopAsync(componentId);
            if (restore)
            {
                await _tasks().RestoreAsync(componentId);
            }
        }
        catch (Exception exception)
        {
            _host.Warn(exception);
        }
    }

    public async Task StartAsync()
    {
        BlockCheck2StrategyEvidenceItem? item = _host.SelectedStrategy;
        if (item == null || _isRunning || _host.IsStrategyTestRunning)
        {
            return;
        }

        _host.UpdateSelectedStrategy();
        var result = _host.GetFullStrategyArguments(item);
        if (!result.Success || string.IsNullOrEmpty(result.Result))
        {
            _host.ShowMessage(
                BlockCheck2NoticeSeverity.Warning,
                _host.Text("ManualTesting"),
                result.ErrorHappens ? result.Error!.ErrorCode : _host.Text("ConfigMakerEmptyArgumentsMessage"));
            return;
        }

        await StopAsync();
        bool componentWasRunning = await _tasks().IsRunningAsync(_componentId());
        if (componentWasRunning && !await _host.ConfirmReplacementAsync())
        {
            return;
        }

        _restoreComponentAfterTest = componentWasRunning;
        await _tasks().StopAsync(_componentId());

        _isRunning = true;
        _isStartingTest = true;
        _tasks().StateUpdated += _stateHandler;
        _host.UpdateSelectedStrategy();

        _host.ShowMessage(
            BlockCheck2NoticeSeverity.Informational,
            _host.Text("ManualTesting"),
            _host.Text("ConfigMakerTestStartedMessage"));

        try
        {
            await _tasks().RunAsync(_componentId(), result.Result);
            _isStartingTest = false;
        }
        catch (Exception exception)
        {
            _isStartingTest = false;
            _host.ShowMessage(
                BlockCheck2NoticeSeverity.Error,
                _host.Text("ManualTesting"),
                string.Format(_host.Text("ConfigMakerTestStartFailedMessage"), exception.Message));
            await StopAsync();
        }
    }

    public void HandleStateUpdated(Tuple<string, bool> state)
    {
        if (!_isRunning || _isStartingTest ||
            !string.Equals(state.Item1, _componentId(), StringComparison.OrdinalIgnoreCase) || state.Item2)
        {
            return;
        }

        _host.Enqueue(async () =>
        {
            _host.ShowMessage(
                BlockCheck2NoticeSeverity.Warning,
                _host.Text("ManualTesting"),
                _host.Text("ConfigMakerTestExitedMessage"));
            await StopAsync();
        });
    }
}
