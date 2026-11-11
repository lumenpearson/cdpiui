#nullable enable

using System;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

// Owns the original config-page test lifecycle; the host supplies preparation and UI effects.
internal sealed class CreateConfigTestSession : ObservableObject
{
    private const string StartTestGlyph = "\uE768";
    private const string StopTestGlyph = "\uE71A";
    private readonly ICreateConfigTestTasks _tasks;
    private readonly Func<(bool HasComponent, string ComponentId)> _selection;
    private readonly Action _convertDesigner;
    private readonly Func<ConfigItem?> _createConfig;
    private readonly Func<ConfigItem, string> _startupArguments;
    private readonly Func<string, string> _localize;
    private readonly Action<Action> _enqueue;
    private bool _isRunning;
    private string _curRunId = string.Empty;
    private string _testButtonText = null!;
    private string _testButtonGlyph = StartTestGlyph;

    public CreateConfigTestSession(
        ICreateConfigTestTasks tasks,
        Func<(bool HasComponent, string ComponentId)> selection,
        Action convertDesigner,
        Func<ConfigItem?> createConfig,
        Func<ConfigItem, string> startupArguments,
        Func<string, string> localize,
        Action<Action> enqueue)
    {
        _tasks = tasks;
        _selection = selection;
        _convertDesigner = convertDesigner;
        _createConfig = createConfig;
        _startupArguments = startupArguments;
        _localize = localize;
        _enqueue = enqueue;
    }

    public bool IsRunning => _isRunning;
    public string CurrentRunId => _curRunId;
    public string TestButtonText
    {
        get => _testButtonText;
        set => SetProperty(ref _testButtonText, value);
    }
    public string TestButtonGlyph
    {
        get => _testButtonGlyph;
        set => SetProperty(ref _testButtonGlyph, value);
    }

    // The host initializes this field, without notification, at its original constructor point.
    public void InitializeText(string text) => _testButtonText = text;

    public void Stop()
    {
        _tasks.StateUpdated -= OnProcessStateChanged;
        if (!_isRunning || string.IsNullOrEmpty(_curRunId))
            return;
        string runId = _curRunId;
        Reset();
        _ = _tasks.StopAsync(runId);
    }

    private void OnProcessStateChanged(Tuple<string, bool> state) =>
        _enqueue(() => ApplyProcessState(state));

    private void ApplyProcessState(Tuple<string, bool> state)
    {
        if (string.IsNullOrEmpty(_curRunId) || state.Item1 != _curRunId)
            return;
        if (state.Item2)
        {
            TestButtonGlyph = StopTestGlyph;
            TestButtonText = _localize("StopTest");
            _isRunning = true;
        }
        else
        {
            TestButtonGlyph = StartTestGlyph;
            TestButtonText = _localize("TestThis");
            _isRunning = false;
        }
    }

    public async Task ToggleAsync()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            _tasks.StateUpdated -= OnProcessStateChanged;
            _tasks.StateUpdated += OnProcessStateChanged;
            var selection = _selection();
            if (!selection.HasComponent)
            {
                Reset();
                return;
            }
            _curRunId = selection.ComponentId;
            await _tasks.StopAsync(_curRunId);
            _convertDesigner();
            var config = _createConfig();
            if (config != null)
                _ = _tasks.RunAsync(_curRunId, _startupArguments(config));
            else
                Reset();
        }
        else
        {
            await _tasks.StopAsync(_curRunId);
            Reset();
        }
    }

    private void Reset()
    {
        TestButtonGlyph = StartTestGlyph;
        TestButtonText = _localize("TestThis");
        _isRunning = false;
        _tasks.StateUpdated -= OnProcessStateChanged;
        _curRunId = string.Empty;
    }
}
