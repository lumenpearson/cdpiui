using System;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;

namespace CDPIUI.ViewModels.CreateConfig;

// The test run of the config in the selected component.
public sealed partial class CreateNewConfigViewModel
{
    private const string StartTestGlyph = "\uE768";
    private const string StopTestGlyph = "\uE71A";

    private bool _isRunning;
    private string _curRunId = string.Empty;
    private string _testButtonText;
    private string _testButtonGlyph = StartTestGlyph;

    public string TestButtonText
    {
        get => _testButtonText;
        private set => SetProperty(ref _testButtonText, value);
    }

    public string TestButtonGlyph
    {
        get => _testButtonGlyph;
        private set => SetProperty(ref _testButtonGlyph, value);
    }

    /// <summary>Stops the test component that kept running after the user left the page.</summary>
    public void StopTestRun()
    {
        // Unsubscribed even when nothing runs: the test component can exit by itself, which
        // clears _isRunning but leaves the handler subscribed.
        ComponentTasksManager.Instance.TaskStateUpdated -= Instance_onProcessStateChanged;

        if (!_isRunning || string.IsNullOrEmpty(_curRunId))
            return;
        string runId = _curRunId;
        ResetTestState();
        _ = ComponentTasksManager.Instance.StopTask(runId);
    }

    private void Instance_onProcessStateChanged(Tuple<string, bool> tuple)
    {
        // The state comes from the pipe thread.
        _dispatcherQueue?.TryEnqueue(() => ApplyProcessState(tuple));
    }

    private void ApplyProcessState(Tuple<string, bool> tuple)
    {
        if (string.IsNullOrEmpty(_curRunId) || tuple.Item1 != _curRunId)
            return;
        if (tuple.Item2)
        {
            TestButtonGlyph = StopTestGlyph;
            TestButtonText = _localizer.GetLocalizedString("StopTest");
            _isRunning = true;
        }
        else
        {
            TestButtonGlyph = StartTestGlyph;
            TestButtonText = _localizer.GetLocalizedString("TestThis");
            _isRunning = false;
        }
    }

    private async Task ToggleTestRunAsync()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            // The handler stays subscribed after the component exited by itself; never add it twice.
            ComponentTasksManager.Instance.TaskStateUpdated -= Instance_onProcessStateChanged;
            ComponentTasksManager.Instance.TaskStateUpdated += Instance_onProcessStateChanged;

            if (SelectedComponent is not ConfigComponentOption component)
            {
                ResetTestState();
                return;
            }
            _curRunId = component.Id;
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
        TestButtonGlyph = StartTestGlyph;
        TestButtonText = _localizer.GetLocalizedString("TestThis");
        _isRunning = false;
        ComponentTasksManager.Instance.TaskStateUpdated -= Instance_onProcessStateChanged;
        _curRunId = string.Empty;
    }
}
