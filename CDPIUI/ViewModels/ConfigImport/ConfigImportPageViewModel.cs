using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Commands;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Shared.Extensions;
using CDPIUI.Views.ConfigImportUtil;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.ConfigImport;

/// <summary>
/// State and logic of the preset import wizard: the installed target components, the import
/// of the chosen files, the missing file step, saving and testing the converted presets.
/// </summary>
public sealed partial class ConfigImportPageViewModel : ObservableObject
{
    private const string LogSource = "MainPage";

    private readonly IConfigImportPageView _view;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ConfigImportService _importService = new();
    private readonly ConfigImportFileAnalyzer _fileAnalyzer;
    private readonly ConfigImportInstaller _installer = new();
    private readonly ConfigImportAutoCorrector _autoCorrector = new();
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly HashSet<ConfigSharePackage> _packagesBeingSaved = [];

    private string _requestedTargetId = string.Empty;
    private bool _isUnloaded;
    private string[] _pendingDroppedFiles;
    private bool _importingDroppedFiles;
    private ConfigImportStep _currentStep = ConfigImportStep.Selection;

    private bool _isPlaceholderVisible;
    private bool _isMainContentVisible = true;
    private bool _isNextEnabled = true;
    private bool _isNextVisible = true;
    private string _nextButtonText;
    private bool _isLoading;
    private string _loadingStateText;
    private bool _areResultsEnabled = true;
    private ConfigImportCompletionState _completionState = ConfigImportCompletionState.None;
    private string _completionTitle = string.Empty;
    private string _completionMessage = string.Empty;

    public ConfigImportPageViewModel(IConfigImportPageView view, DispatcherQueue dispatcherQueue)
    {
        _view = view;
        _dispatcherQueue = dispatcherQueue;
        _fileAnalyzer = new ConfigImportFileAnalyzer(_importService);
        // The texts the button and the loading panel show before the first change. The button
        // has no localization uid: the uid would reset the text of the later steps.
        _nextButtonText = _localizer.GetLocalizedString("GoForwardButton.Content");
        _loadingStateText = _localizer.GetLocalizedString("WorkingOnIt");

        NextCommand = new AsyncRelayCommand(NextAsync, () => _isNextEnabled);
        OpenComponentsStoreCommand = new RelayCommand(() => CommandsHandler.HandleCommand("cdpiui://Store/Category/C001CS"));

        LoadComponents();
    }

    public AsyncRelayCommand NextCommand { get; }

    public RelayCommand OpenComponentsStoreCommand { get; }

    public ObservableCollection<DatabaseStoreItem> Components { get; } = [];

    public ObservableCollection<ConfigImportPresetViewModel> Results { get; } = [];

    public ObservableCollection<ConfigImportMissingFileViewModel> MissingFiles { get; } = [];

    public bool IsPlaceholderVisible
    {
        get => _isPlaceholderVisible;
        private set => SetProperty(ref _isPlaceholderVisible, value);
    }

    public bool IsMainContentVisible
    {
        get => _isMainContentVisible;
        private set => SetProperty(ref _isMainContentVisible, value);
    }

    public bool IsNextVisible
    {
        get => _isNextVisible;
        private set => SetProperty(ref _isNextVisible, value);
    }

    public string NextButtonText
    {
        get => _nextButtonText;
        private set => SetProperty(ref _nextButtonText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string LoadingStateText
    {
        get => _loadingStateText;
        private set => SetProperty(ref _loadingStateText, value);
    }

    public bool AreResultsEnabled
    {
        get => _areResultsEnabled;
        private set => SetProperty(ref _areResultsEnabled, value);
    }

    public ConfigImportCompletionState CompletionState
    {
        get => _completionState;
        private set => SetProperty(ref _completionState, value);
    }

    public string CompletionTitle
    {
        get => _completionTitle;
        private set => SetProperty(ref _completionTitle, value);
    }

    public string CompletionMessage
    {
        get => _completionMessage;
        private set => SetProperty(ref _completionMessage, value);
    }

    private void SetNextEnabled(bool value)
    {
        _isNextEnabled = value;
        NextCommand.NotifyCanExecuteChanged();
    }

    private void GoToStep(ConfigImportStep step)
    {
        _currentStep = step;
        _view.GoToStep(step);
    }

    public void OnNavigatedTo(string requestedTargetId)
    {
        _isUnloaded = false;
        _requestedTargetId = requestedTargetId ?? string.Empty;
    }

    public async Task OnLoadedAsync()
    {
        _isUnloaded = false;
        StoreHelper.Instance.ItemActionsStopped -= StoreComponentsChanged;
        StoreHelper.Instance.ItemRemoved -= StoreComponentsChanged;
        StoreHelper.Instance.ItemActionsStopped += StoreComponentsChanged;
        StoreHelper.Instance.ItemRemoved += StoreComponentsChanged;
        LoadComponents();
        await ImportPendingFilesAsync();
    }

    public async Task OnUnloadedAsync()
    {
        _isUnloaded = true;
        StoreHelper.Instance.ItemActionsStopped -= StoreComponentsChanged;
        StoreHelper.Instance.ItemRemoved -= StoreComponentsChanged;
        await StopActiveTestAsync();
        foreach (var item in Results)
            if (item.Result.SharedPackage != null && !_packagesBeingSaved.Contains(item.Result.SharedPackage))
                item.Result.SharedPackage.Dispose();
    }

    private void LoadComponents()
    {
        Components.Clear();
        foreach (DatabaseStoreItem item in DatabaseHelper.Instance.GetItemsByType("component")
                     .Where(ConfigImportTargetFactory.IsImportTarget)
                     .OrderBy(item => item.ShortName.NullIfEmpty() ?? item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            item.ShortName = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
            Components.Add(item);
        }
        if (Results.Count == 0)
        {
            bool available = Components.Count > 0;
            IsPlaceholderVisible = !available;
            IsMainContentVisible = available;
            SetNextEnabled(available);
        }
    }

    private void StoreComponentsChanged(string id) => _dispatcherQueue.TryEnqueue(() =>
    {
        if (!_isUnloaded && Results.Count == 0)
            LoadComponents();
    });

    private async Task NextAsync()
    {
        // An exception in an async void handler ended the process and left IsLoading set.
        try
        {
            await NextStepAsync();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
            if (_isUnloaded)
                return;
            IsLoading = false;
            AreResultsEnabled = true;
            IsNextVisible = true;
            if (_view.IsViewLoaded)
                await _view.ShowErrorAsync(exception.Message);
        }
    }

    private async Task NextStepAsync()
    {
        if (_currentStep == ConfigImportStep.Selection)
        {
            IsNextVisible = false;
            await ImportConfigsAsync();
            if (_isUnloaded)
                return;
            IsNextVisible = true;
        }
        else if (_currentStep == ConfigImportStep.MissingFiles)
        {
            IsNextVisible = true;
            bool flg = false;
            foreach (var item in MissingFiles)
            {
                if (!item.IsResolved && item.HasSuggestion)
                {
                    item.UseSuggestion();
                }
                if (!item.IsResolved && !item.HasSuggestion)
                {
                    SetNextEnabled(false);
                    flg = true;
                }
            }
            if (!flg)
                ShowCompletion();
        }
        else if (_currentStep == ConfigImportStep.Completed)
        {
            IsNextVisible = false;
            AreResultsEnabled = false;

            IsLoading = true;
            LoadingStateText = _localizer.GetLocalizedString("Saving");

            List<ConfigImportPresetViewModel> itemsToRemove = [];
            foreach (var item in Results.ToList())
            {
                if (item.CanSave)
                {
                    if (await SaveItemAsync(item))
                        itemsToRemove.Add(item);
                }
            }

            foreach (var item in itemsToRemove)
            {
                if (string.IsNullOrEmpty(item.OperationError))
                {
                    Results.Remove(item);
                }
            }

            itemsToRemove.Clear();

            IsLoading = false;
            AreResultsEnabled = true;

            if (Results.Count == 0)
            {
                GoToStep(ConfigImportStep.WorkDone);
            }
        }
    }
}
