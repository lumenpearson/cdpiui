using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.MainPage;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using CDPIUI.Shared.Extensions;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.ComponentViews;

/// <summary>
/// State of one component tile on the main page: the title, the process state, the
/// selected configuration with its quick settings and the linked features. Every tile
/// has its own instance.
/// </summary>
public sealed partial class ComponentTileViewModel : ObservableObject
{
    private const string LogSource = "ComponentTileUserControl";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IComponentTileView _view;

    private string _storeId = string.Empty;
    private string _title = string.Empty;
    private bool _isAutorunBadgeVisible;
    private string _accentColor;
    private string _stateToggleText = string.Empty;
    private bool _isRunning;
    private string _statusBrushKey = "SystemFillColorSuccessBrush";
    private string _statusGlyph = "\uF13E";
    private double _statusGlyphFontSize = 18;
    private string _statusToolTip;
    private bool _isConfigContentVisible = true;
    private bool _isConfigErrorVisible;
    private bool _isConfigSettingsLoadFailureVisible = true;
    private ConfigSelectorItem _selectedConfig;

    public ComponentTileViewModel(DispatcherQueue dispatcherQueue, IComponentTileView view)
    {
        _dispatcherQueue = dispatcherQueue;
        _view = view;

        // The command methods catch and log their own errors.
        ToggleProcessCommand = new RelayCommand(() => _ = ToggleProcessAsync());
        ViewOutputCommand = new RelayCommand(() => _ = ViewOutputAsync());
        ViewSettingsCommand = new RelayCommand(() => _ = ViewSettingsAsync());

        ViewOutputText = _localizer.GetLocalizedString("ViewOutputIconButton");
        SettingsText = _localizer.GetLocalizedString("SettingsIconButton");
        LinkedFeaturesGlyph = SharedUtils.IsOsSupportedNewGlyph() ? "\uF4A5" : "\uE8B0";

        ComponentTasksManager.Instance.TaskStateUpdated += TaskUpdated;
    }

    public ICommand ToggleProcessCommand { get; }

    public ICommand ViewOutputCommand { get; }

    public ICommand ViewSettingsCommand { get; }

    public ObservableCollection<ConfigSelectorItem> ConfigItems { get; } = [];

    public ObservableCollection<ConfigSettingsItem> ConfigSettings { get; } = [];

    public ObservableCollection<FeaturesViewModel> AvailableFeatures { get; } = [];

    public string ViewOutputText { get; }

    public string SettingsText { get; }

    public string LinkedFeaturesGlyph { get; }

    public string StoreId
    {
        get => _storeId;
        set => _storeId = value;
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public bool IsAutorunBadgeVisible
    {
        get => _isAutorunBadgeVisible;
        private set => SetProperty(ref _isAutorunBadgeVisible, value);
    }

    /// <summary>The background color of the component from the store, as a hex string.</summary>
    public string AccentColor
    {
        get => _accentColor;
        private set => SetProperty(ref _accentColor, value);
    }

    public string StateToggleText
    {
        get => _stateToggleText;
        private set => SetProperty(ref _stateToggleText, value);
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

    public string StatusGlyph
    {
        get => _statusGlyph;
        private set => SetProperty(ref _statusGlyph, value);
    }

    public double StatusGlyphFontSize
    {
        get => _statusGlyphFontSize;
        private set => SetProperty(ref _statusGlyphFontSize, value);
    }

    public string StatusToolTip
    {
        get => _statusToolTip;
        private set => SetProperty(ref _statusToolTip, value);
    }

    public bool IsConfigContentVisible
    {
        get => _isConfigContentVisible;
        private set => SetProperty(ref _isConfigContentVisible, value);
    }

    public bool IsConfigErrorVisible
    {
        get => _isConfigErrorVisible;
        private set => SetProperty(ref _isConfigErrorVisible, value);
    }

    public bool IsConfigSettingsLoadFailureVisible
    {
        get => _isConfigSettingsLoadFailureVisible;
        private set => SetProperty(ref _isConfigSettingsLoadFailureVisible, value);
    }

    public ConfigSelectorItem SelectedConfig
    {
        get => _selectedConfig;
        private set => SetProperty(ref _selectedConfig, value);
    }

    /// <summary>Stops listening to the task manager and the configuration list.</summary>
    public void Detach()
    {
        ComponentTasksManager.Instance.TaskStateUpdated -= TaskUpdated;

        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(_storeId);
        if (componentHelper is null)
            return;

        componentHelper.ConfigListUpdated -= LoadConfigItems;
    }

    public async Task InitAsync()
    {
        try
        {
            Title = DatabaseHelper.Instance.GetItemById(_storeId)?.ShortName.NullIfEmpty() ?? _storeId;

            if (DatabaseHelper.Instance.IsItemInstalled(_storeId))
            {
                // PreferTaskStateActionsAsync and CheckComponentStateAsync catch and log their own errors.
                _ = PreferTaskStateActionsAsync();

                AccentColor = DatabaseHelper.Instance.GetItemById(_storeId).BackgroundColor;

                IsAutorunBadgeVisible = SettingsManager.Instance.GetValue<bool>(["CONFIGS", _storeId], "usedForAutorun");
                _ = CheckComponentStateAsync();

                string storeId = _storeId;

                LoadAvailableFeatures();

                await Task.Run(() => LoadConfigInfo(storeId));
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void LoadAvailableFeatures()
    {
        AvailableFeatures.Clear();
        FeaturesData.AvailableFeaturesForComponent.TryGetValue(_storeId, out var availableFeatures);
        if (availableFeatures == null)
            return;

        foreach (var feature in availableFeatures)
        {
            FeaturesData.AvailableComponentFeatureImages.TryGetValue(feature, out var imageSource);
            if (imageSource == null)
                continue;

            AvailableFeatures.Add(new()
            {
                DisplayName = _localizer.GetLocalizedString(feature.ToString()),
                Id = feature,
                Image = new Uri(imageSource)
            });
        }
    }

    private void TaskUpdated(Tuple<string, bool> tuple)
    {
        // Can be raised on a thread pool thread (BlockCheck2 and preset tests stop and start
        // tasks there); the bound controls are updated on the UI thread only.
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (tuple.Item1 != _storeId)
                return;
            _ = PreferTaskStateActionsAsync(); // PreferTaskStateActionsAsync catches and logs its own errors.
        });
    }

    private async Task PreferTaskStateActionsAsync()
    {
        try
        {
            bool isRunning = await ComponentTasksManager.Instance.IsTaskRunning(_storeId);

            if (isRunning)
            {
                StateToggleText = _localizer.GetLocalizedString("Stop");
                ShowComponentState(ComponentState.Running);
            }
            else
            {
                StateToggleText = _localizer.GetLocalizedString("Start");
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
        ComponentStateVisual visual = ComponentStateVisual.For(componentState);
        if (visual == null)
            return;

        StatusBrushKey = visual.BackgroundBrushKey;
        StatusGlyph = visual.Glyph;
        StatusGlyphFontSize = visual.FontSize;
        StatusToolTip = _localizer.GetLocalizedString(visual.ToolTipKey);
    }

    private async Task ViewOutputAsync()
    {
        try
        {
            await _view.ShowOutputWindowAsync(_storeId);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task ViewSettingsAsync()
    {
        try
        {
            await _view.ShowComponentSettingsAsync(_storeId);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void ApplySavedSelection()
    {
        var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", _storeId], "configFile");
        var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", _storeId], "configId");

        if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
        {
            ClearConfigSettings();
            return;
        }

        var match = ConfigItems.FirstOrDefault(ci => ci.FileName == savedFile && ci.PackId == savedPackId);
        if (match == null)
        {
            // The saved preset is gone (deleted or renamed): drop its quick settings.
            ClearConfigSettings();
            return;
        }

        // Raise even when the value is the same: the combo box only takes the selection
        // once the item is in its list.
        if (!SetProperty(ref _selectedConfig, match, nameof(SelectedConfig)))
            OnPropertyChanged(nameof(SelectedConfig));
    }

    /// <summary>Shows the empty quick settings state, as InitConfigSettings does before loading.</summary>
    private void ClearConfigSettings()
    {
        // Forget the stale selection too, so a late quick setting toggle or settings load
        // does not act on a preset that is no longer in the list.
        SelectedConfig = null;
        ConfigSettings.Clear();
        IsConfigSettingsLoadFailureVisible = true;
    }
}
