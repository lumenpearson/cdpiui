using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.MainPage;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Features;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using CDPIUI.Shared.Extensions;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;
using StoreComponents = CDPIUI.Core.Store.Data.Components;

namespace CDPIUI.ViewModels.ComponentViews;

/// <summary>
/// State of one small component tile: the process state from the task manager, the
/// autorun choice and the texts of the buttons. Every tile has its own instance.
/// </summary>
public sealed partial class SmallComponentTileViewModel : ObservableObject
{
    private const string LogSource = "SmallComponentTileUserControl";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Func<string, Task> _showOutputAsync;

    private string _storeId = string.Empty;
    private bool _isOpened;
    private string _showMoreSettingsToolTip = string.Empty;
    private bool _isProxyButtonVisible = true;
    private bool? _isAutorunChecked = false;
    private string _playToolTip = string.Empty;
    private string _playGlyph = "\uE768";
    private bool _isRunning;
    private string _statusBrushKey = "SystemFillColorSuccessBrush";
    private double _runningGlyphFontSize = 10;
    private bool _isRunningGlyphVisible = true;
    private string _stopGlyph = "\uE7E8";
    private bool _isStopGlyphVisible = true;
    private string _statusToolTip;

    public SmallComponentTileViewModel(DispatcherQueue dispatcherQueue, Func<string, Task> showOutputAsync)
    {
        _dispatcherQueue = dispatcherQueue;
        _showOutputAsync = showOutputAsync;

        // The command methods catch and log their own errors.
        ToggleProcessCommand = new RelayCommand(() => _ = ToggleProcessAsync());
        ViewOutputCommand = new RelayCommand(() => _ = ViewOutputAsync());
        SaveAutorunCommand = new RelayCommand(SaveAutorun);
    }

    public ICommand ToggleProcessCommand { get; }

    public ICommand ViewOutputCommand { get; }

    public ICommand SaveAutorunCommand { get; }

    public string StoreId
    {
        get => _storeId;
        set => _storeId = value;
    }

    /// <summary>Whether the component settings below the tile are shown.</summary>
    public bool IsOpened
    {
        get => _isOpened;
        set
        {
            _isOpened = value;
            UpdateShowMoreSettingsToolTip();
        }
    }

    public string ShowMoreSettingsToolTip
    {
        get => _showMoreSettingsToolTip;
        private set => SetProperty(ref _showMoreSettingsToolTip, value);
    }

    public bool IsProxyButtonVisible
    {
        get => _isProxyButtonVisible;
        private set => SetProperty(ref _isProxyButtonVisible, value);
    }

    public bool? IsAutorunChecked
    {
        get => _isAutorunChecked;
        set => SetProperty(ref _isAutorunChecked, value);
    }

    public string PlayToolTip
    {
        get => _playToolTip;
        private set => SetProperty(ref _playToolTip, value);
    }

    public string PlayGlyph
    {
        get => _playGlyph;
        private set => SetProperty(ref _playGlyph, value);
    }

    /// <summary>
    /// The checked state of the start and stop button. Always raised: the button also
    /// toggles itself when clicked, and the state must win over the click.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            _isRunning = value;
            OnPropertyChanged();
        }
    }

    public string StatusBrushKey
    {
        get => _statusBrushKey;
        private set => SetProperty(ref _statusBrushKey, value);
    }

    public double RunningGlyphFontSize
    {
        get => _runningGlyphFontSize;
        private set => SetProperty(ref _runningGlyphFontSize, value);
    }

    public bool IsRunningGlyphVisible
    {
        get => _isRunningGlyphVisible;
        private set => SetProperty(ref _isRunningGlyphVisible, value);
    }

    public string StopGlyph
    {
        get => _stopGlyph;
        private set => SetProperty(ref _stopGlyph, value);
    }

    public bool IsStopGlyphVisible
    {
        get => _isStopGlyphVisible;
        private set => SetProperty(ref _isStopGlyphVisible, value);
    }

    public string StatusToolTip
    {
        get => _statusToolTip;
        private set => SetProperty(ref _statusToolTip, value);
    }

    /// <summary>Runs on every component change.</summary>
    public void Init()
    {
        // Init runs on every StoreId change; the handler was added again each time.
        Attach();

        IsProxyButtonVisible = HardcodedItemIds.ComponentIds.GetKeyByValue(_storeId) == StoreComponents.TgWsProxy;

        if (DatabaseHelper.Instance.IsItemInstalled(_storeId))
        {
            // PreferTaskStateActionsAsync and CheckComponentStateAsync catch and log their own errors.
            _ = PreferTaskStateActionsAsync();

            IsAutorunChecked = SettingsManager.Instance.GetValue<bool>(["CONFIGS", _storeId], "usedForAutorun");
            _ = CheckComponentStateAsync();
        }
        UpdateShowMoreSettingsToolTip();
    }

    /// <summary>Called when the tile is loaded again.</summary>
    public void Reload()
    {
        if (string.IsNullOrEmpty(_storeId))
            return;
        Attach();
        _ = PreferTaskStateActionsAsync(); // PreferTaskStateActionsAsync catches and logs its own errors.
    }

    /// <summary>
    /// ComponentTasksManager outlives the tile: the handler is held only while it is loaded.
    /// </summary>
    public void Detach()
    {
        ComponentTasksManager.Instance.TaskStateUpdated -= TaskUpdated;
    }

    private void Attach()
    {
        ComponentTasksManager.Instance.TaskStateUpdated -= TaskUpdated;
        ComponentTasksManager.Instance.TaskStateUpdated += TaskUpdated;
    }

    private void TaskUpdated(Tuple<string, bool> tuple)
    {
        // Raised on the pipe thread; the bound controls are updated on the UI thread only.
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (tuple.Item1 != _storeId)
                return;
            _ = PreferTaskStateActionsAsync(); // PreferTaskStateActionsAsync catches and logs its own errors.
        });
    }

    private void UpdateShowMoreSettingsToolTip()
    {
        ShowMoreSettingsToolTip = _isOpened
            ? _localizer.GetLocalizedString("ShowLessComponentSettingsText")
            : _localizer.GetLocalizedString("ShowMoreComponentSettingsText");
    }

    private async Task CheckComponentStateAsync()
    {
        try
        {
            ShowComponentState(await ComponentStateReader.GetStateAsync(_storeId));
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void ShowComponentState(ComponentState componentState)
    {
        IsRunningGlyphVisible = true;
        IsStopGlyphVisible = false;
        switch (componentState)
        {
            case ComponentState.SetupRequired:
                ShowStoppedState("SystemFillColorSolidNeutralBrush", "\uE713", "ComponentStatusSetupRequired");
                break;
            case ComponentState.Stopped:
                ShowStoppedState("SystemFillColorCriticalBrush", "\uE7E8", "ComponentStatusStopped");
                break;
            case ComponentState.Running:
                StatusBrushKey = "SystemFillColorSuccessBrush";
                RunningGlyphFontSize = 18;
                StatusToolTip = _localizer.GetLocalizedString("ComponentStatusStarted");
                break;
            case ComponentState.ExitedWithException:
                ShowStoppedState("SystemFillColorCautionBrush", "\uEDAE", "ComponentStatusExceptionHappens");
                break;
        }
    }

    private void ShowStoppedState(string brushKey, string glyph, string toolTipKey)
    {
        StatusBrushKey = brushKey;
        StopGlyph = glyph;
        IsRunningGlyphVisible = false;
        IsStopGlyphVisible = true;
        StatusToolTip = _localizer.GetLocalizedString(toolTipKey);
    }

    private async Task PreferTaskStateActionsAsync()
    {
        try
        {
            bool isRunning = await ComponentTasksManager.Instance.IsTaskRunning(_storeId);

            if (isRunning)
            {
                PlayToolTip = _localizer.GetLocalizedString("Stop");
                PlayGlyph = SharedUtils.IsOsSupportedNewGlyph() ? "\uE62E" : "\uF8AE";
                ShowComponentState(ComponentState.Running);
            }
            else
            {
                PlayToolTip = _localizer.GetLocalizedString("Start");
                PlayGlyph = "\uF5B0";
                _ = CheckComponentStateAsync(); // CheckComponentStateAsync catches and logs its own errors.
            }

            IsRunning = isRunning;
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task ToggleProcessAsync()
    {
        try
        {
            bool isRunning = await ComponentTasksManager.Instance.IsTaskRunning(_storeId);

            if (!isRunning)
            {
                _ = ComponentTasksManager.Instance.CreateAndRunNewTask(_storeId);
            }
            else
            {
                await ComponentTasksManager.Instance.StopTask(_storeId);
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task ViewOutputAsync()
    {
        try
        {
            await _showOutputAsync(_storeId);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void SaveAutorun()
    {
        bool isChecked = (bool)_isAutorunChecked;
        SettingsManager.Instance.SetValue<bool>(["CONFIGS", _storeId], "usedForAutorun", isChecked);
        if (isChecked)
            ApplicationAutorunManager.AddToAutorun();
    }
}
