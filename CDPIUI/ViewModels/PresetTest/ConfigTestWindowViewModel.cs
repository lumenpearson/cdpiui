using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper;
using CDPIUI.Helper.AddOns;
using CDPIUI.Helper.Parsers;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.PresetTest;

/// <summary>
/// State and commands of the preset test window: the component and preset choice, the test
/// run with its progress, the ranked results and applying a result.
/// </summary>
public sealed partial class ConfigTestWindowViewModel : ObservableObject
{
    private const string LogSource = "ConfigTestWindow";
    private const string HelpCommandUri = "cdpiui://Help/Autoselection/BestConfigSelection/";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly IConfigTestWindowView _view;

    private CancellationTokenSource _testCts;
    private PresetTestHelper _activeHelper;
    private PresetTestType _lastTestType = PresetTestType.Standard;
    private string _testedComponentId = string.Empty;
    private bool _isTesting;
    private bool _isClosed;

    private string _componentIdToTest = string.Empty;
    private ViewComponentModel _selectedComponent;
    private List<ConfigItem> _presets = new();
    private bool? _isDpiTestSelected = false;
    private bool? _isSelectedPresetsMode = false;
    private bool _hasResults;
    private bool _isProgressVisible;
    private double _progressValue;
    private string _progressStatusText = string.Empty;
    private string _progressEtaText = string.Empty;

    public ConfigTestWindowViewModel(IConfigTestWindowView view)
    {
        _view = view;
        StartTestCommand = new AsyncRelayCommand(StartTestAsync, () => CanStartTest, LogSource);
        StopTestCommand = new RelayCommand(CancelTest, () => _isTesting);
        OpenTargetsFileCommand = new RelayCommand(OpenTargetsFile);
        OpenHelpCommand = new RelayCommand(() => CommandsHandler.HandleCommand(HelpCommandUri));
    }

    public AsyncRelayCommand StartTestCommand { get; }

    public RelayCommand StopTestCommand { get; }

    public RelayCommand OpenTargetsFileCommand { get; }

    public RelayCommand OpenHelpCommand { get; }

    public ObservableCollection<ViewComponentModel> Components { get; } = new();

    public ObservableCollection<PresetResultViewModel> ResultItems { get; } = new();

    /// <summary>Store id of the component to select; setting it reloads the component list.</summary>
    public string ComponentIdToTest
    {
        get => _componentIdToTest;
        set
        {
            _componentIdToTest = value;
            LoadComponents();
        }
    }

    public ViewComponentModel SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (SetProperty(ref _selectedComponent, value))
                Presets = GetPresetsForSelectedComponent();
        }
    }

    public List<ConfigItem> Presets
    {
        get => _presets;
        private set => SetProperty(ref _presets, value);
    }

    public bool? IsDpiTestSelected
    {
        get => _isDpiTestSelected;
        set => SetProperty(ref _isDpiTestSelected, value);
    }

    public bool? IsSelectedPresetsMode
    {
        get => _isSelectedPresetsMode;
        set
        {
            if (SetProperty(ref _isSelectedPresetsMode, value))
                OnPropertyChanged(nameof(IsPresetListEnabled));
        }
    }

    /// <summary>The test settings can be changed: no test is running.</summary>
    public bool IsIdle => !_isTesting;

    public bool IsPresetListEnabled => !_isTesting && IsSelectedPresetsMode == true;

    public bool AreResultsVisible => _hasResults;

    public bool IsResultsPlaceholderVisible => !_hasResults;

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        private set => SetProperty(ref _isProgressVisible, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string ProgressStatusText
    {
        get => _progressStatusText;
        private set => SetProperty(ref _progressStatusText, value);
    }

    public string ProgressEtaText
    {
        get => _progressEtaText;
        private set => SetProperty(ref _progressEtaText, value);
    }

    private bool CanStartTest => !_isTesting && Components.Count > 0;

    public void LoadComponents()
    {
        try
        {
            UIHelper.LoadInstalledComponentsList(Components);

            Components.Remove(Components.FirstOrDefault(x => x.StoreId == "CSTYFL050"));

            if (Components.Count > 0)
                SelectedComponent = Components.FirstOrDefault(x => x.StoreId == ComponentIdToTest) ?? Components.First();
            StartTestCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(LogSource, $"Can't load components: {ex.Message}");
        }
    }

    /// <summary>Called when the window closes: a test must not keep switching presets in the background.</summary>
    public void OnWindowClosed()
    {
        _isClosed = true;
        CancelTest();
    }

    private List<ConfigItem> GetPresetsForSelectedComponent()
    {
        if (SelectedComponent is not ViewComponentModel entry)
            return new List<ConfigItem>();

        try
        {
            ComponentItemsLoaderHelper.Instance.Init();
            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(entry.StoreId);
            if (componentHelper == null)
                return new List<ConfigItem>();

            ConfigurationService configHelper = componentHelper.GetConfigHelper();
            if (configHelper == null)
                return new List<ConfigItem>();

            return configHelper.GetConfigItems().Where(c => c != null && !c.MarkAsRemoved).ToList();
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(LogSource, $"Can't load presets: {ex.Message}");
            return new List<ConfigItem>();
        }
    }

    private async Task StartTestAsync()
    {
        if (_isTesting)
            return;

        if (SelectedComponent is not ViewComponentModel entry)
            return;

        List<ConfigItem> presets;
        if (IsSelectedPresetsMode == true)
        {
            presets = _view.GetSelectedPresets().ToList();
            if (presets.Count == 0)
            {
                AppendLine(new TestLogSegment($"[{_localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                           new TestLogSegment(_localizer.GetLocalizedString("PT_NoSelection")));
                return;
            }
        }
        else
        {
            presets = GetPresetsForSelectedComponent();
        }

        _lastTestType = IsDpiTestSelected == true ? PresetTestType.DpiChecker : PresetTestType.Standard;

        ProcessService processManager = (await ComponentTasksManager.Instance.GetTaskFromId(entry.StoreId))?.ProcessManager;
        if (processManager == null)
        {
            AppendLine(new TestLogSegment($"[{_localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                       new TestLogSegment(_localizer.GetLocalizedString("PT_ComponentNotFound")));
            return;
        }

        _view.ClearLog();
        ClearResults();
        SetTesting(true);
        _testCts = new CancellationTokenSource();
        _activeHelper = new PresetTestHelper();

        _testedComponentId = entry.StoreId;

        var logProgress = new Progress<List<TestLogSegment>>(AppendLine);
        var testProgress = new Progress<PresetTestProgress>(UpdateProgress);

        try
        {
            PresetTestRunResult runResult = await Task.Run(() =>
                _activeHelper.RunAsync(entry.StoreId, processManager, _lastTestType, presets, logProgress, testProgress, _testCts.Token));

            if (runResult.WasCancelled)
                AppendLine(new TestLogSegment(_localizer.GetLocalizedString("PT_Cancelled"), TestLogColor.Yellow));

            if (!_isClosed)
                ShowResults(runResult, _lastTestType);
        }
        catch (Exception ex)
        {
            // RunAsync has already stopped the tested preset and restored the component.
            AppendLine(new TestLogSegment($"[{_localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                       new TestLogSegment(ex.Message, TestLogColor.Red));
        }
        finally
        {
            if (!_isClosed)
            {
                SetTesting(false);
                HideProgress();
            }
            _activeHelper = null;
            _testCts?.Dispose();
            _testCts = null;
        }
    }

    private void CancelTest()
    {
        try
        {
            _activeHelper?.CancelAllCurls();
            _testCts?.Cancel();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    /// <summary>Makes the preset of a result the component's preset; restarts or starts the component as asked.</summary>
    public async Task ApplyPresetAsync(PresetResultViewModel result, bool restartIfRunning)
    {
        if (result?.Preset == null || string.IsNullOrEmpty(_testedComponentId))
            return;

        try
        {
            SettingsManager.Instance.SetValue<string>(["CONFIGS", _testedComponentId], "configFile", result.Preset.file_name);
            SettingsManager.Instance.SetValue<string>(["CONFIGS", _testedComponentId], "configId", result.Preset.packId);

            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(_testedComponentId);
            componentHelper.ReInitConfigs();

            AppendLine(new TestLogSegment(string.Format(_localizer.GetLocalizedString("PT_PresetApplied"), result.PresetName), TestLogColor.Green));

            if (restartIfRunning)
            {
                if (await ComponentTasksManager.Instance.IsTaskRunning(_testedComponentId))
                    await ComponentTasksManager.Instance.RestartTask(_testedComponentId);
                else
                    _ = ComponentTasksManager.Instance.CreateAndRunNewTask(_testedComponentId);
            }
            else if (await ComponentTasksManager.Instance.IsTaskRunning(_testedComponentId))
            {
                await ComponentTasksManager.Instance.RestartTask(_testedComponentId);
            }
        }
        catch (Exception ex)
        {
            AppendLine(new TestLogSegment($"[{_localizer.GetLocalizedString("PT_Warn")}] ", TestLogColor.Yellow),
                       new TestLogSegment(ex.Message, TestLogColor.Red));
        }
    }

    private void ShowResults(PresetTestRunResult runResult, PresetTestType testType)
    {
        ResultItems.Clear();
        if (runResult?.Ranked == null || runResult.Ranked.Count == 0)
        {
            SetHasResults(false);
            return;
        }

        string applyLabel = _localizer.GetLocalizedString("PresetTestApplyButton");
        string runLabel = _localizer.GetLocalizedString("PresetTestRunButton");
        string recommended = _localizer.GetLocalizedString("PresetTestRecommendedBadge");
        ConfigItem bestPreset = runResult.Best?.Preset;

        foreach (PresetTestResult r in runResult.Ranked)
        {
            bool isBest = bestPreset != null && r.Preset != null &&
                r.Preset.file_name == bestPreset.file_name &&
                r.Preset.packId == bestPreset.packId;

            ResultItems.Add(new PresetResultViewModel
            {
                Preset = r.Preset,
                PresetName = r.PresetName,
                PackName = string.IsNullOrEmpty(r.PackName) ? r.Preset?.file_name : r.PackName,
                MetricsText = r.GetMetricsSummary(testType, _localizer),
                IsBest = isBest,
                RecommendedBadge = recommended,
                ApplyLabel = applyLabel,
                RunLabel = runLabel
            });
        }

        SetHasResults(true);
    }

    private void ClearResults()
    {
        ResultItems.Clear();
        SetHasResults(false);
    }

    private void SetHasResults(bool hasResults)
    {
        _hasResults = hasResults;
        // The old code hid the visible element first, then showed the other one.
        if (hasResults)
            OnPropertiesChanged(nameof(IsResultsPlaceholderVisible), nameof(AreResultsVisible));
        else
            OnPropertiesChanged(nameof(AreResultsVisible), nameof(IsResultsPlaceholderVisible));
    }

    private void UpdateProgress(PresetTestProgress p)
    {
        if (p == null || _isClosed)
            return;

        IsProgressVisible = true;
        ProgressValue = p.Percent;
        ProgressStatusText = string.Format(
            _localizer.GetLocalizedString("PT_ProgressStatus"),
            p.CurrentIndex, p.TotalPresets, p.CurrentPresetName);

        if (p.EstimatedRemaining.HasValue && p.CompletedPresets > 0 && p.CompletedPresets < p.TotalPresets)
            ProgressEtaText = UnitsParser.ConvertMinutesToPrettyText(p.EstimatedRemaining.Value.TotalMinutes);
        else
            ProgressEtaText = _localizer.GetLocalizedString("Calculating");
    }

    private void HideProgress()
    {
        IsProgressVisible = false;
        ProgressValue = 0;
        ProgressStatusText = string.Empty;
        ProgressEtaText = string.Empty;
    }

    private static void OpenTargetsFile()
    {
        try
        {
            PresetTestHelper.EnsureTargetsFile();
            Process.Start(new ProcessStartInfo
            {
                FileName = PresetTestHelper.GetTargetsFilePath(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(LogSource, $"Can't open targets file: {ex.Message}");
        }
    }

    private void SetTesting(bool testing)
    {
        _isTesting = testing;
        StartTestCommand.NotifyCanExecuteChanged();
        StopTestCommand.NotifyCanExecuteChanged();
        OnPropertiesChanged(nameof(IsIdle), nameof(IsPresetListEnabled));

        if (testing)
            IsProgressVisible = true;
    }

    private void AppendLine(List<TestLogSegment> segments)
    {
        if (segments == null || _isClosed)
            return;
        _view.AppendLogLine(segments);
    }

    private void AppendLine(params TestLogSegment[] segments)
    {
        AppendLine(segments.ToList());
    }
}
