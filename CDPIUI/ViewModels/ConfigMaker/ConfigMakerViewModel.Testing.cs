using System;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The test run of the edited command in the selected component.
public sealed partial class ConfigMakerViewModel
{
    private bool _isTesting;
    private bool _isStartingTest;
    private bool _restoreComponentAfterTest;
    private string _testComponentId = string.Empty;

    public event EventHandler TestStateChanged;

    public bool IsTesting => _isTesting;

    public async Task StartTestAsync()
    {
        if (string.IsNullOrWhiteSpace(ComponentId))
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerSelectComponentMessage"),
                InfoBarSeverity.Warning);
            return;
        }

        if (!TryResolveTestArguments(out string arguments))
        {
            return;
        }

        await StopTestAsync();
        bool componentWasRunning = await ComponentTasksManager.Instance.IsTaskRunning(ComponentId);
        if (componentWasRunning && !await ConfirmReplaceRunningComponentAsync())
        {
            return;
        }

        _restoreComponentAfterTest = componentWasRunning;
        await ComponentTasksManager.Instance.StopTask(ComponentId);

        _testComponentId = ComponentId;
        _isTesting = true;
        _isStartingTest = true;
        ComponentTasksManager.Instance.TaskStateUpdated += ComponentTasksManager_TaskStateUpdated;
        RaiseTestStateChanged();

        if (!DatabaseHelper.Instance.IsItemInstalled(_testComponentId))
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("TargetComponentNotInstalled"),
                InfoBarSeverity.Error);
            await StopTestAsync();
            return;
        }

        ShowEditorMessage(
            _localizer.GetLocalizedString("ConfigMakerTestStartedMessage"),
            InfoBarSeverity.Informational);

        try
        {
            await ComponentTasksManager.Instance.CreateAndRunNewTask(_testComponentId, arguments);
            _isStartingTest = false;
            await _view.RefreshOutputAsync();
        }
        catch (Exception exception)
        {
            _isStartingTest = false;
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerTestStartFailedMessage"),
                    exception.Message),
                InfoBarSeverity.Error);
            await StopTestAsync();
        }
    }

    public async Task StopTestAsync(bool showCompletionMessage = false)
    {
        if (!_isTesting)
        {
            return;
        }

        string componentId = _testComponentId;
        bool restore = _restoreComponentAfterTest;
        _isTesting = false;
        _isStartingTest = false;
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;
        RaiseTestStateChanged();

        if (!string.IsNullOrWhiteSpace(componentId))
        {
            await ComponentTasksManager.Instance.StopTask(componentId);
            if (restore)
            {
                await ComponentTasksManager.Instance.CreateAndRunNewTask(componentId);
            }
        }

        _testComponentId = string.Empty;
        _restoreComponentAfterTest = false;
        if (showCompletionMessage)
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerTestStoppedMessage"),
                InfoBarSeverity.Informational);
        }
    }

    private bool TryResolveTestArguments(out string arguments)
    {
        try
        {
            SyncDocumentFromEditor();
            arguments = ComponentCommandLineFormatter.ToSingleLine(
                ConfigMakerPresetStorageService.CreateResolvedCommandForTest(_presetDocument));
        }
        catch (Exception exception)
        {
            arguments = string.Empty;
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerResolvePresetFailedMessage"),
                    exception.Message),
                InfoBarSeverity.Error);
            return false;
        }
        if (string.IsNullOrWhiteSpace(arguments))
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerEmptyArgumentsMessage"),
                InfoBarSeverity.Warning);
            return false;
        }
        return true;
    }

    private Task<bool> ConfirmReplaceRunningComponentAsync() => _view.ConfirmAsync(
        _localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentTitle"),
        _localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentMessage"),
        _localizer.GetLocalizedString("Continue"));

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!_isTesting || _isStartingTest ||
            !string.Equals(state.Item1, _testComponentId, StringComparison.OrdinalIgnoreCase) ||
            state.Item2)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(async () =>
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerTestExitedMessage"),
                InfoBarSeverity.Warning);
            await StopTestAsync();
        });
    }

    private void RaiseTestStateChanged()
    {
        OnPropertyChanged(nameof(IsTesting));
        TestStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
