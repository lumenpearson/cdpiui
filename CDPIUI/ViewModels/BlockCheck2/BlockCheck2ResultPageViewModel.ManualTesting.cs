using System;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared.Models;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2ResultPageViewModel
{
    public bool IsManualTestRunning => _manualTest.IsRunning;

    /// <summary>Runs after a click on the toggle button, which has already changed its state.</summary>
    public Task ToggleManualTestFromButtonAsync() => _manualTest.ToggleFromButtonAsync();

    public Task ToggleManualTestAsync() => _manualTest.ToggleAsync();

    public Task StopTestAsync(bool showCompletionMessage = false) => _manualTest.StopAsync(showCompletionMessage);

    private Task StopTestOnLeaveAsync() => _manualTest.StopOnLeaveAsync();

    public Task StartTestAsync() => _manualTest.StartAsync();

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state) => _manualTest.HandleStateUpdated(state);

    private static string GetManualTestComponentId() => HardcodedItemIds.ComponentIds[Components.Zapret2];

    private static IConfigMakerTestTasks GetManualTestTasks() =>
        new BlockCheck2ManualComponentTestTasks(ComponentTasksManager.Instance);

    private sealed class ManualTestHost(BlockCheck2ResultPageViewModel page) : IBlockCheck2ManualTestHost
    {
        public BlockCheck2StrategyEvidenceItem? SelectedStrategy => page._view.SelectedStrategy;
        public bool IsStrategyTestRunning => page._strategyTestRunning;
        public bool? IsManualTestChecked => page.IsManualTestChecked;
        public OperationResultModel<string> GetFullStrategyArguments(BlockCheck2StrategyEvidenceItem item) =>
            page.Result.GetFullStrategyArguments(item);
        public Task<bool> ConfirmReplacementAsync() => page._view.ConfirmReplaceRunningComponentAsync();
        public void UpdateSelectedStrategy() => page.UpdateSelectedStrategy();
        public void UpdateAssignmentView() => page.UpdateAssignmentView();
        public string Text(string key) => page.Text(key);
        public void ShowMessage(BlockCheck2NoticeSeverity severity, string title, string message) =>
            page.ShowActionMessage(severity, title, message);
        public void Warn(Exception exception) =>
            Logger.Instance.CreateWarningLog(LogSource, $"Cannot stop the manual test: {exception}");
        public void Enqueue(Func<Task> callback) => page._dispatcherQueue.TryEnqueue(async () => await callback());
    }
}
