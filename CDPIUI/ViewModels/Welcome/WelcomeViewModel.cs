using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.Store;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Welcome;

/// <summary>
/// State of the welcome wizard: license, Store proxy check, ready kits and the GoodbyeDPI
/// migration flow. The window keeps the step navigation and the button animations.
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject
{
    private const string LogSource = "WelcomeWindow";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly IWelcomeView _view;
    private readonly DispatcherQueue _dispatcherQueue;

    private bool _isNextEnabled = true;
    private bool _isLoading;
    private string? _licenseText;
    private bool _isLicenseAgreeEnabled = true;
    private bool? _isLicenseAccepted = false;
    private bool? _showAppFeatures = true;
    private bool _isStoreStatusCheckIdle = true;
    private string _storeStatusText = string.Empty;
    private bool _isStoreAvailable;
    private bool _isStoreErrorVisible = true;
    private bool _isStoreKitsVisible = true;

    public WelcomeViewModel(IWelcomeView view, DispatcherQueue dispatcherQueue)
    {
        _view = view;
        _dispatcherQueue = dispatcherQueue;
        CheckStoreStatusCommand = new RelayCommand(
            () => _ = CheckStoreStatusAsync(), // CheckStoreStatusAsync catches and logs its own errors.
            () => _isStoreStatusCheckIdle);
        RetryMigrationCommand = new AsyncRelayCommand(RetryMigrationAsync, logSource: LogSource);
    }

    public RelayCommand CheckStoreStatusCommand { get; }

    public AsyncRelayCommand RetryMigrationCommand { get; }

    public ObservableCollection<StoreViewBundleItem> Kits { get; } = [];

    public bool IsNextEnabled
    {
        get => _isNextEnabled;
        set => SetProperty(ref _isNextEnabled, value);
    }

    /// <summary>The loading state of the wizard buttons bar.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string? LicenseText
    {
        get => _licenseText;
        private set => SetProperty(ref _licenseText, value);
    }

    public bool IsLicenseAgreeEnabled
    {
        get => _isLicenseAgreeEnabled;
        private set => SetProperty(ref _isLicenseAgreeEnabled, value);
    }

    /// <summary>Bound two-way to the license check box; only the user changes it.</summary>
    public bool? IsLicenseAccepted
    {
        get => _isLicenseAccepted;
        set
        {
            if (!SetProperty(ref _isLicenseAccepted, value))
                return;
            IsNextEnabled = value ?? false;
            if (value == true)
                SettingsManager.Instance.SetValue("WELCOMEWIZARD", "Shown", true);
        }
    }

    public bool? ShowAppFeatures
    {
        get => _showAppFeatures;
        set => SetProperty(ref _showAppFeatures, value);
    }

    public string StoreStatusText
    {
        get => _storeStatusText;
        private set => SetProperty(ref _storeStatusText, value);
    }

    public bool IsStoreAvailable
    {
        get => _isStoreAvailable;
        private set => SetProperty(ref _isStoreAvailable, value);
    }

    public bool IsStoreErrorVisible
    {
        get => _isStoreErrorVisible;
        private set => SetProperty(ref _isStoreErrorVisible, value);
    }

    public bool IsStoreKitsVisible
    {
        get => _isStoreKitsVisible;
        private set => SetProperty(ref _isStoreKitsVisible, value);
    }

    /// <summary>Called when the user leaves the license step with Next.</summary>
    public bool TryAcceptLicenseStep()
    {
        if (IsLicenseAccepted != true)
            return false;
        SettingsManager.Instance.SetValue("WELCOMEWIZARD", "Shown", true);
        return true;
    }

    public void EnterLicenseStep()
    {
        LoadLicense();
        IsNextEnabled = IsLicenseAccepted ?? false;
    }

    private void LoadLicense()
    {
        string path = Path.Combine(Directories.ELUADirectory, StoreLocalizationHelper.GetStoreLikeLocale(), "ELUA.md");
        try
        {
            LicenseText = ShellHelper.LoadAllTextFromFile(path);
            IsLicenseAgreeEnabled = true;
        }
        catch
        {
            IsLicenseAgreeEnabled = false;
            LicenseText = string.Format(_localizer.GetLocalizedString("/WelcomeWizard/UnableLoadLicense"), path);
        }
    }

    public async Task CheckStoreStatusAsync()
    {
        try
        {
            SetStoreStatusCheckIdle(false);
            IsLoading = true;
            await Task.Delay(500);
            bool result = await StoreHelper.Instance.IsServerAvailable();
            IsLoading = false;
            SetStoreStatusCheckIdle(true);
            StoreStatusText = result ? _localizer.GetLocalizedString("Available") : _localizer.GetLocalizedString("NotAvailable");
            IsStoreAvailable = result;
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void SetStoreStatusCheckIdle(bool value)
    {
        if (_isStoreStatusCheckIdle == value)
            return;
        _isStoreStatusCheckIdle = value;
        CheckStoreStatusCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadStoreAsync()
    {
        try
        {
            IsStoreErrorVisible = false;
            IsStoreKitsVisible = false;

            _view.ShowStoreLoadingButtons();
            IsLoading = true;

            bool result = await StoreHelper.Instance.LoadAllStoreDatabase();

            if (result)
                result = LoadReadyKits();

            IsStoreErrorVisible = !result;
            IsStoreKitsVisible = result;

            if (_view.CurrentStep == WelcomeStep.Store)
            {
                IsLoading = false;
                _view.ShowStoreLoadingButtons();
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private bool LoadReadyKits()
    {
        Kits.Clear();
        List<ReadyKitModel> kits = StoreHelper.Instance.ReadyKits
            .OrderByDescending(kit => kit.IsRecommended)
            .ToList();

        if (kits.Count == 0)
        {
            Kits.Clear();
            return false;
        }
        foreach (var kit in kits.Select(ReadyKitViewModelFactory.Create))
            Kits.Add(kit);

        return true;
    }

    /// <summary>Queues the items of the selected kits that are not installed yet.</summary>
    public async Task DownloadKitsAsync(IReadOnlyList<StoreViewBundleItem> selectedKits)
    {
        try
        {
            foreach (StoreViewBundleItem kit in selectedKits)
            {
                var readyKit = StoreHelper.Instance.GetReadyKitFromStoreId(kit.KitId);
                if (readyKit == null)
                    continue;

                var items = (readyKit.items ?? [])
                    .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
                    .OfType<RepoItemModel>()
                    .ToList();

                List<RepoItemModel> itemsToInstall = items
                    .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id))
                    .ToList();
                if (itemsToInstall.Count == 0)
                    continue;

                List<ItemLicenseModel> licenses = StoreItemLicenses.CollectDistinct(itemsToInstall);

                if (licenses.Count > 0 && !await _view.AcceptLicensesAsync(licenses))
                    return;

                HashSet<string> pendingIds = itemsToInstall
                    .Select(item => item.store_id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string itemId in readyKit.items ?? [])
                {
                    if (pendingIds.Contains(itemId))
                        StoreHelper.Instance.AddItemToQueue(itemId, string.Empty);
                }
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }
}
