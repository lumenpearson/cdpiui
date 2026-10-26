using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class ResultPage
{
    private bool _isStartingTest = false;
    private bool _isManualTestRunning = false;
    private bool _restoreComponentAfterTest = false;

    private async void ComponentManualTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (ComponentManualTestButton.IsChecked == true)
        {
            await StartTestAsync();
        }
        else
        {
            await StopTestAsync(true);
        }
    }

    private async void ComponentManualTestMenuItem_Click(object sender, RoutedEventArgs e)
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
                InfoBarSeverity.Informational,
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
            Logger.Instance.CreateWarningLog(nameof(ResultPage), $"Cannot stop the manual test: {exception}");
        }
    }

    public async Task StartTestAsync()
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null || _isManualTestRunning || _strategyTestRunning)
        {
            return;
        }

        UpdateSelectedStrategy();

        var result = ViewModel.GetFullStrategyArguments(item);

        if (!result.Success || string.IsNullOrEmpty(result.Result))
        {
            ShowActionMessage(
                InfoBarSeverity.Warning,
                Text("ManualTesting"),
                result.ErrorHappens ? result.Error.ErrorCode : Text("ConfigMakerEmptyArgumentsMessage"));
            return;
        }

        await StopTestAsync();
        bool componentWasRunning = await ComponentTasksManager.Instance.IsTaskRunning(HardcodedItemIds.ComponentIds[Components.Zapret2]);
        if (componentWasRunning && !await ConfirmReplaceRunningComponentAsync())
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
            InfoBarSeverity.Informational,
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
                InfoBarSeverity.Error,
                Text("ManualTesting"),
                string.Format(
                    Text("ConfigMakerTestStartFailedMessage"),
                    exception.Message));
            await StopTestAsync();
        }
    }

    private async Task<bool> ConfirmReplaceRunningComponentAsync()
    {
        ContentDialog confirmation = new()
        {
            XamlRoot = XamlRoot,
            Title = _localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentTitle"),
            Content = _localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentMessage"),
            PrimaryButtonText = _localizer.GetLocalizedString("Continue"),
            CloseButtonText = _localizer.GetLocalizedString("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await confirmation.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!_isManualTestRunning || _isStartingTest ||
            !string.Equals(state.Item1, HardcodedItemIds.ComponentIds[Components.Zapret2], StringComparison.OrdinalIgnoreCase) ||
            state.Item2)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(async () =>
        {
            ShowActionMessage(
                InfoBarSeverity.Warning,
                Text("ManualTesting"),
                _localizer.GetLocalizedString("ConfigMakerTestExitedMessage"));
            await StopTestAsync();
        });
    }
}
