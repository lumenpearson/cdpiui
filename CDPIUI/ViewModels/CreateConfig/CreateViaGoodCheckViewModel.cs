using System;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Data;
using CDPIUI.Shared.Extensions;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>
/// The state of the wizard that creates a config with GoodCheck: the current step, the
/// loading state, the site lists, the strategy lists and the GoodCheck settings.
/// </summary>
public sealed partial class CreateViaGoodCheckViewModel : ObservableObject
{
    private const string LogSource = "CreateViaGoodCheck";
    private const string AddOnId = "ASGKOI001";

    private readonly ICreateViaGoodCheckView _view;
    private readonly DispatcherQueue _uiDispatcher;
    private readonly ILocalizer _localizer = Localizer.Get();

    private GoodCheckWizardState _currentState = GoodCheckWizardState.None;
    private bool _isLoading;
    private bool _isLoadingIndeterminate = true;
    private bool _isCancellationAvailable;
    private string _loadingHeader = string.Empty;
    private bool _canGoForward = true;
    private bool _isChangeModeEnabled = true;

    public CreateViaGoodCheckViewModel(ICreateViaGoodCheckView view)
    {
        _view = view;
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
        InitResolverItems();
    }

    public string ComponentId { get; private set; } = string.Empty;

    public GoodCheckWizardState CurrentState => _currentState;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    public bool IsNotLoading => !IsLoading;

    public bool IsLoadingIndeterminate
    {
        get => _isLoadingIndeterminate;
        private set => SetProperty(ref _isLoadingIndeterminate, value);
    }

    public bool IsCancellationAvailable
    {
        get => _isCancellationAvailable;
        private set => SetProperty(ref _isCancellationAvailable, value);
    }

    public string LoadingHeader
    {
        get => _loadingHeader;
        private set => SetProperty(ref _loadingHeader, value);
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        private set => SetProperty(ref _canGoForward, value);
    }

    /// <summary>The user can pick a strategy list for each site list.</summary>
    public bool IsChangeModeEnabled
    {
        get => _isChangeModeEnabled;
        private set => SetProperty(ref _isChangeModeEnabled, value);
    }

    public void SetComponent(string componentId)
    {
        ComponentId = componentId;
    }

    /// <summary>Shows the first step for the component.</summary>
    public void Start()
    {
        if (string.IsNullOrEmpty(ComponentId))
            return;

        if (ComponentId == HardcodedItemIds.ComponentIds[Components.GoodbyeDPI])
        {
            IsChangeModeEnabled = false;
        }

        _view.HideAllPanels();
        SetLoadingMode(false);
        SwitchState(GoodCheckWizardState.LetsBegin);
    }

    public void GoBack()
    {
        switch (_currentState)
        {
            case GoodCheckWizardState.SiteListsSettings:
                CanGoForward = true;
                SwitchState(GoodCheckWizardState.LetsBegin, slideToLeft: false);
                break;
            case GoodCheckWizardState.LetsBegin:
                _view.LeaveWizard();
                break;
            default:
                SwitchState(_currentState - 1, slideToLeft: false);
                break;
        }
    }

    public async Task GoForwardAsync()
    {
        switch (_currentState)
        {
            case GoodCheckWizardState.LetsBegin:
                SwitchState(GoodCheckWizardState.PrepareLoading);
                await InitGoodCheckSiteListsPage();
                break;
            case GoodCheckWizardState.SiteListsSettings:
                SwitchState(GoodCheckWizardState.StrategyListSettings);
                InitGoodCheckStrategiesListsPage();
                CanGoForward = SelectedStrategiesList != null || IsSeparateMode;
                break;
            case GoodCheckWizardState.StrategyListSettings:
                SwitchState(GoodCheckWizardState.AdditionalSettings);
                LoadGoodCheckSettings();
                break;
            case GoodCheckWizardState.AdditionalSettings:
                SaveGoodCheckSettings();
                _view.OpenWorkPage();
                StartGoodCheck();
                break;
            default:
                SwitchState(_currentState + 1);
                break;
        }
    }

    private void SetLoadingMode(
        bool isLoading,
        bool isIndeterminate = true,
        bool cancellationAvailable = false,
        string headerText = ""
        )
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => SetLoadingMode(isLoading, isIndeterminate, cancellationAvailable, headerText));
            return;
        }

        if (isLoading)
        {
            IsLoadingIndeterminate = isIndeterminate;
            LoadingHeader = headerText;
            IsCancellationAvailable = cancellationAvailable;
        }
        IsLoading = isLoading;
    }

    private void SwitchState(GoodCheckWizardState newState, bool slideToLeft = true)
    {
        if (newState == _currentState)
            return;

        if (!_view.SwitchPanel(_currentState, newState, slideToLeft))
            return;

        _currentState = newState;
    }

    private async Task ShowErrorDialog(string message = null)
    {
        try
        {
            await _view.ShowErrorAsync(string.IsNullOrEmpty(message)
                ? string.Format(_localizer.GetLocalizedString("AddOnNotInstalled"), HardcodedItemIds.AddOnsIds.GetKeyByValue(AddOnId), AddOnId)
                : message);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }
}
