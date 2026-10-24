using System;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using Microsoft.UI.Xaml;

namespace CDPIUI.Views.CreateConfigHelper;

// The test run of the config in the selected component.
public sealed partial class CreateNewConfigPage
{
    private void Instance_onProcessStateChanged(Tuple<string, bool> tuple)
    {
        // The state comes from the pipe thread.
        DispatcherQueue?.TryEnqueue(() => ApplyProcessState(tuple));
    }

    private void ApplyProcessState(Tuple<string, bool> tuple)
    {
        if (string.IsNullOrEmpty(_curRunId) || tuple.Item1 != _curRunId)
            return;
        if (tuple.Item2)
        {
            TestButtonGlyph.Glyph = "\uE71A";
            TestButtonText.Text = _localizer.GetLocalizedString("StopTest");
            _isRunning = true;
        }
        else
        {
            TestButtonGlyph.Glyph = "\uE768";
            TestButtonText.Text = _localizer.GetLocalizedString("TestThis");
            _isRunning = false;
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            _isRunning = true;
            ComponentTasksManager.Instance.TaskStateUpdated += Instance_onProcessStateChanged;

            if (ComponentChooseComboBox.SelectedItem is not ComponentModel model)
            {
                ResetTestState();
                return;
            }
            _curRunId = model.Id;
            await ComponentTasksManager.Instance.StopTask(_curRunId);
            ConvertDesignerLikeSettingsToString();
            var config = CreateConfig(0, saveLocalization: false);
            if (config != null)
                _ = ComponentTasksManager.Instance.CreateAndRunNewTask(_curRunId, ConfigurationService.GetStartupParametersByConfigItem(config));
            else
                ResetTestState();
        }
        else
        {
            await ComponentTasksManager.Instance.StopTask(_curRunId);
            ResetTestState();
        }
    }

    private void ResetTestState()
    {
        TestButtonGlyph.Glyph = "\uE768";
        TestButtonText.Text = _localizer.GetLocalizedString("TestThis");
        _isRunning = false;
        ComponentTasksManager.Instance.TaskStateUpdated -= Instance_onProcessStateChanged;
        _curRunId = string.Empty;
    }

    // The test component kept running after the user left the page.
    private void StopTestRun()
    {
        if (!_isRunning || string.IsNullOrEmpty(_curRunId))
            return;
        string runId = _curRunId;
        ResetTestState();
        _ = ComponentTasksManager.Instance.StopTask(runId);
    }
}
