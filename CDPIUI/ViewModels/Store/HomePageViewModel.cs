using System;
using System.IO;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;

namespace CDPIUI.ViewModels.Store;

/// <summary>
/// State of the Store home page: loading the catalogue and showing the loading, error or
/// catalogue panel.
/// </summary>
public sealed partial class HomePageViewModel : ObservableObject
{
    private const string LogSource = "HomePage";

    private readonly Action _showCategories;

    private bool _isLoadingVisible = true;
    private bool _isErrorVisible;
    private bool _isStoreVisible;
    private string _errorCode = "FLASHLIGHT_BUSY";

    /// <param name="showCategories">Builds the catalogue in the view after the database is loaded.</param>
    public HomePageViewModel(Action showCategories)
    {
        _showCategories = showCategories;
    }

    public bool IsLoadingVisible
    {
        get => _isLoadingVisible;
        private set => SetProperty(ref _isLoadingVisible, value);
    }

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set => SetProperty(ref _isErrorVisible, value);
    }

    public bool IsStoreVisible
    {
        get => _isStoreVisible;
        private set => SetProperty(ref _isStoreVisible, value);
    }

    public string ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    /// <summary>Subscribes to the Store errors.</summary>
    public void Attach()
    {
        StoreHelper.Instance.StoreInternalErrorHappens += StoreHelper_ErrorHappens;
    }

    /// <summary>Unsubscribes from the Store errors; called when the page is left.</summary>
    public void Detach()
    {
        StoreHelper.Instance.StoreInternalErrorHappens -= StoreHelper_ErrorHappens;
    }

    /// <summary>Loads the Store database and shows the catalogue or the error panel.</summary>
    public async Task LoadAsync()
    {
        try
        {
            // The catalogue is parsed again only when the daily sync is due, nothing is loaded yet,
            // or the Store cache was cleaned up (Memory settings delete the catalogue folder).
            TimeSpan sinceSync = DateTime.UtcNow - SettingsManager.Instance.GetValue<DateTime>("STORE", "lastSyncTime");
            bool cacheMissing = !Directory.Exists(CDPIUI.Core.Data.Directories.StoreRepoCacheDirectory);
            bool result = await StoreHelper.Instance.LoadAllStoreDatabase(forseSync: sinceSync.TotalDays >= 1 || cacheMissing);
            if (result)
            {
                _showCategories();

                IsErrorVisible = false;
                IsLoadingVisible = false;
                IsStoreVisible = true;
            }
            else
            {
                IsErrorVisible = true;
                IsLoadingVisible = false;
                IsStoreVisible = false;
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void StoreHelper_ErrorHappens(string obj)
    {
        IsErrorVisible = true;
        ErrorCode = obj;
        IsLoadingVisible = false;
        IsStoreVisible = false;
    }
}
