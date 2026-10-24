using System;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Data;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2ResultPageViewModel
{
    private bool _isStartingTest;
    private bool _isManualTestRunning;
    private bool _restoreComponentAfterTest;

    public bool IsManualTestRunning => _isManualTestRunning;

    /// <summary>Runs after a click on the toggle button, which has already changed its state.</summary>
    public async Task ToggleManualTestFromButtonAsync()
    {
        if (IsManualTestChecked == true)
        {
            await StartTestAsync();
        }
        else
        {
            await StopTestAsync(true);
        }
    }

    public async Task ToggleManualTestAsync()
    {
        if (_isManualTestRunning)
        {
            await StopTestAsync(showCompletionMessage: true);
        }
        else
        {
            await StartTestAsync();
        }
    }

    public async Task StopTestAsync(bool showCompletionMessage = false)
    {
        UpdateSelectedStrategy();
        if (!_isManualTestRunning)
        {
            return;
        }

        bool restore = _restoreComponentAfterTest;
        _isManualTestRunning = false;
        _isStartingTest = false;
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;

        if (!string.IsNullOrWhiteSpace(HardcodedItemIds.ComponentIds[Components.Zapret2]))
        {
            await ComponentTasksManager.Instance.StopTask(HardcodedItemIds.ComponentIds[Components.Zapret2]);
            if (restore)
            {
                await ComponentTasksManager.Instance.CreateAndRunNewTask(HardcodedItemIds.ComponentIds[Components.Zapret2]);
            }
        }

        _restoreComponentAfterTest = false;
        if (showCompletionMessage)
        {
            ShowActionMessage(
                BlockCheck2NoticeSeverity.Informational,
                Text("ManualTesting"),
                Text("ConfigMakerTestStoppedMessage"));
        }

        UpdateSelectedStrategy();
        UpdateAssignmentView();
    }

    private async Task StopTestOnLeaveAsync()
    {
        if (!_isManualTestRunning)
        {
            return;
        }

        bool restore = _restoreComponentAfterTest;
        _isManualTestRunning = false;
        _isStartingTest = false;
        _restoreComponentAfterTest = false;
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;

        string componentId = HardcodedItemIds.ComponentIds[Components.Zapret2];
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return;
        }
        try
        {
            await ComponentTasksManager.Instance.StopTask(componentId);
            if (restore)
            {
                await ComponentTasksManager.Instance.CreateAndRunNewTask(componentId);
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, $"Cannot stop the manual test: {exception}");
        }
    }

    public async Task StartTestAsync()
    {
        BlockCheck2StrategyEvidenceItem? item = _view.SelectedStrategy;
        if (item == null || _isManualTestRunning || _strategyTestRunning)
        {
            return;
        }

        UpdateSelectedStrategy();

        var result = Result.GetFullStrategyArguments(item);

        if (!result.Success || string.IsNullOrEmpty(result.Result))
        {
            ShowActionMessage(
                BlockCheck2NoticeSeverity.Warning,
                Text("ManualTesting"),
                result.ErrorHappens ? result.Error.ErrorCode : Text("ConfigMakerEmptyArgumentsMessage"));
            return;
        }

        await StopTestAsync();
        bool componentWasRunning = await ComponentTasksManager.Instance.IsTaskRunning(HardcodedItemIds.ComponentIds[Components.Zapret2]);
        if (componentWasRunning && !await _view.ConfirmReplaceRunningComponentAsync())
        {
            return;
        }

        _restoreComponentAfterTest = componentWasRunning;
        await ComponentTasksManager.Instance.StopTask(HardcodedItemIds.ComponentIds[Components.Zapret2]);

        _isManualTestRunning = true;
        _isStartingTest = true;
        ComponentTasksManager.Instance.TaskStateUpdated += ComponentTasksManager_TaskStateUpdated;
        UpdateSelectedStrategy();

        ShowActionMessage(
            BlockCheck2NoticeSeverity.Informational,
            Text("ManualTesting"),
            Text("ConfigMakerTestStartedMessage"));

        try
        {
            await ComponentTasksManager.Instance.CreateAndRunNewTask(HardcodedItemIds.ComponentIds[Components.Zapret2], result.Result);
            _isStartingTest = false;
        }
        catch (Exception exception)
        {
            _isStartingTest = false;
            ShowActionMessage(
                BlockCheck2NoticeSeverity.Error,
                Text("ManualTesting"),
                string.Format(
                    Text("ConfigMakerTestStartFailedMessage"),
                    exception.Message));
            await StopTestAsync();
        }
    }

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!_isManualTestRunning || _isStartingTest ||
            !string.Equals(state.Item1, HardcodedItemIds.ComponentIds[Components.Zapret2], StringComparison.OrdinalIgnoreCase) ||
            state.Item2)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(async () =>
        {
            ShowActionMessage(
                BlockCheck2NoticeSeverity.Warning,
                Text("ManualTesting"),
                Text("ConfigMakerTestExitedMessage"));
            await StopTestAsync();
        });
    }
}
