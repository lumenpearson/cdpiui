using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Helper.AddOns.ConfigShare;
using CDPIUI.Shared.Extensions;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUI3Localizer;

namespace CDPIUI.Views.ConfigImportUtil;

public sealed partial class MainPage : TemplatePage
{
    private readonly ObservableCollection<DatabaseStoreItem> _components = [];
    private readonly ObservableCollection<ConfigImportPresetViewModel> _results = [];
    private readonly ObservableCollection<ConfigImportMissingFileViewModel> _missingFiles = [];
    private readonly ConfigImportService _importService = new();
    private readonly ConfigImportFileAnalyzer _fileAnalyzer;
    private readonly ConfigImportInstaller _installer = new();
    private readonly ConfigImportAutoCorrector _autoCorrector = new();
    private readonly ILocalizer _localizer = Localizer.Get();

    private string _requestedTargetId = string.Empty;
    private ConfigImportPresetViewModel _activeTestItem;
    private string _activeTestComponentId = string.Empty;
    private ConfigImportTestPlaceholder _activeTestPlaceholder;
    private bool _isUnloaded;
    private string[] _pendingDroppedFiles;
    private bool _importingDroppedFiles;
    private readonly HashSet<ConfigSharePackage> _packagesBeingSaved = [];

    public MainPage()
    {
        _fileAnalyzer = new ConfigImportFileAnalyzer(_importService);
        InitializeComponent();
        LoadComponents();
        ResultItemsControl.ItemsSource = _results;
        MissingFilesItemsControl.ItemsSource = _missingFiles;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isUnloaded = false;
        _requestedTargetId = Parameter.Get("componentId") ?? string.Empty;
    }

    private void LoadComponents()
    {
        _components.Clear();
        foreach (DatabaseStoreItem item in DatabaseHelper.Instance.GetItemsByType("component")
                     .Where(ConfigImportTargetFactory.IsImportTarget)
                     .OrderBy(item => item.ShortName.NullIfEmpty() ?? item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            item.ShortName = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
            _components.Add(item);
        }
        if (_results.Count == 0)
        {
            bool available = _components.Count > 0;
            PlaceholderGrid.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
            MainContent.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            NextButton.IsEnabled = available;
        }
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isUnloaded = false;
        StoreHelper.Instance.ItemActionsStopped -= StoreComponentsChanged;
        StoreHelper.Instance.ItemRemoved -= StoreComponentsChanged;
        StoreHelper.Instance.ItemActionsStopped += StoreComponentsChanged;
        StoreHelper.Instance.ItemRemoved += StoreComponentsChanged;
        LoadComponents();
        await ImportPendingFilesAsync();
    }

    private void StoreComponentsChanged(string id) => DispatcherQueue.TryEnqueue(() =>
    {
        if (!_isUnloaded && _results.Count == 0)
            LoadComponents();
    });

    private void OpenComponentsStore_Click(object sender, RoutedEventArgs e) =>
        CDPIUI.Commands.CommandsHandler.HandleCommand("cdpiui://Store/Category/C001CS");

    private async void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isUnloaded = true;
        StoreHelper.Instance.ItemActionsStopped -= StoreComponentsChanged;
        StoreHelper.Instance.ItemRemoved -= StoreComponentsChanged;
        await StopActiveTestAsync();
        foreach (var item in _results)
            if (item.Result.SharedPackage != null && !_packagesBeingSaved.Contains(item.Result.SharedPackage))
                item.Result.SharedPackage.Dispose();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).OpenWindows.OfType<ConfigImportUtilWindow>()
            .FirstOrDefault(window => window.Content?.XamlRoot == XamlRoot)?.Close();
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        // An exception in an async void handler ended the process and left IsLoading set.
        try
        {
            await NextStepAsync();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(MainPage), exception.ToString());
            if (_isUnloaded)
                return;
            UtilityButtons.IsLoading = false;
            ResultItemsControl.IsEnabled = true;
            NextButton.Visibility = Visibility.Visible;
            if (IsLoaded)
                await new CDPIUI.Controls.Dialogs.Universal.ErrorContentDialog().ShowErrorDialogAsync(
                    _localizer.GetLocalizedString("ConfigShareError"), exception.Message, XamlRoot);
        }
    }

    private async Task NextStepAsync()
    {
        if (MainContent.SelectedItem == SelectionStep)
        {
            NextButton.Visibility = Visibility.Collapsed;
            await ImportConfigsAsync();
            if (_isUnloaded)
                return;
            NextButton.Visibility = Visibility.Visible;
        }
        else if (MainContent.SelectedItem == MissingFilesStep)
        {
            NextButton.Visibility = Visibility.Visible;
            bool flg = false;
            foreach (var item in _missingFiles)
            {
                if (!item.IsResolved && item.HasSuggestion)
                {
                    item.UseSuggestion();
                }
                if (!item.IsResolved && !item.HasSuggestion)
                {
                    NextButton.IsEnabled = false;
                    flg = true;
                }
            }
            if (!flg)
                ShowCompletion();
        }
        else if (MainContent.SelectedItem == CompletedStep)
        {
            NextButton.Visibility = Visibility.Collapsed;
            ResultItemsControl.IsEnabled = false;

            UtilityButtons.IsLoading = true;
            UtilityButtons.LoadingStateText = _localizer.GetLocalizedString("Saving");

            List<ConfigImportPresetViewModel> itemsToRemove = [];
            foreach (var item in _results.ToList())
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
                    _results.Remove(item);
                }
            }

            itemsToRemove.Clear();

            UtilityButtons.IsLoading = false;
            ResultItemsControl.IsEnabled = true;

            if (_results.Count == 0)
            {
                MainContent.GoTo(WorkDoneStep);
            }
        }

    }
}
