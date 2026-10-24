using System;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using CDPIUI.Shared.Extensions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Store;

/// <summary>
/// State of the window that installs a Store item pack file: the item card read from the
/// pack, the install progress and its result.
/// </summary>
public sealed partial class StoreLocalItemInstallingDialogViewModel : ObservableObject
{
    private const string LogSource = "StoreLocalItemInstallingDialog";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _installCancellation = new();

    private string _storeId = string.Empty;
    private string _name = string.Empty;
    private string _packFilePath = string.Empty;
    private bool _isErrorHappens;
    private bool _isClosed;

    private bool _isLoading = true;
    private bool _isGetEnabled;
    private bool _isViewInStoreEnabled;
    private string _itemName = string.Empty;
    private ImageSource _itemIcon;
    private Brush _itemColor;
    private string _developerText = string.Empty;
    private string _versionText = string.Empty;
    private string _categoryText = string.Empty;
    private bool _isDescriptionVisible = true;
    private bool _isErrorVisible;
    private string _errorCode = string.Empty;
    private bool _isDownloadProgressVisible = true;
    private string _statusText;
    private bool _isSpeedVisible = true;
    private string _speedText = string.Empty;
    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private bool _isGetVisible = true;
    private string _cancelButtonText;

    public StoreLocalItemInstallingDialogViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _statusText = _localizer.GetLocalizedString("SatusGettingPackInfo");
    }

    public string StoreId => _storeId;

    /// <summary><see langword="true"/> while the pack is read: the placeholders are shown.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsGetEnabled
    {
        get => _isGetEnabled;
        private set => SetProperty(ref _isGetEnabled, value);
    }

    public bool IsViewInStoreEnabled
    {
        get => _isViewInStoreEnabled;
        private set => SetProperty(ref _isViewInStoreEnabled, value);
    }

    public string ItemName
    {
        get => _itemName;
        private set => SetProperty(ref _itemName, value);
    }

    public ImageSource ItemIcon
    {
        get => _itemIcon;
        private set => SetProperty(ref _itemIcon, value);
    }

    public Brush ItemColor
    {
        get => _itemColor;
        private set => SetProperty(ref _itemColor, value);
    }

    public string DeveloperText
    {
        get => _developerText;
        private set => SetProperty(ref _developerText, value);
    }

    public string VersionText
    {
        get => _versionText;
        private set => SetProperty(ref _versionText, value);
    }

    public string CategoryText
    {
        get => _categoryText;
        private set => SetProperty(ref _categoryText, value);
    }

    public bool IsDescriptionVisible
    {
        get => _isDescriptionVisible;
        private set => SetProperty(ref _isDescriptionVisible, value);
    }

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set => SetProperty(ref _isErrorVisible, value);
    }

    public string ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    public bool IsDownloadProgressVisible
    {
        get => _isDownloadProgressVisible;
        private set => SetProperty(ref _isDownloadProgressVisible, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsSpeedVisible
    {
        get => _isSpeedVisible;
        private set => SetProperty(ref _isSpeedVisible, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool IsGetVisible
    {
        get => _isGetVisible;
        private set => SetProperty(ref _isGetVisible, value);
    }

    /// <summary>
    /// The text of the cancel button once it changes. The button also has a localization uid,
    /// so the view copies this value to it each time it is set, as the window did before.
    /// </summary>
    public string CancelButtonText
    {
        get => _cancelButtonText;
        private set
        {
            _cancelButtonText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Reads the pack file and shows the item it contains.</summary>
    public async Task SetPackFilePathAsync(string packFile)
    {
        try
        {
            if (!string.IsNullOrEmpty(_packFilePath))
                return; // TODO: Show notification : "Only one operation..."

            _packFilePath = packFile;
            ConnectHandlers();
            var model = await LocalItemsInstallerHelper.Instance.ImportStoreItemPackFile(_packFilePath, LocalItemsInstallerHelper_ErrorHappens);
            if (_isClosed)
                return;

            try
            {

                if (model != null)
                {
                    _storeId = model.StoreId;
                    bool isInstalled = DatabaseHelper.Instance.IsItemInstalled(_storeId);

                    _name = model.ShortName.NullIfEmpty() ?? StoreHelper.Instance.GetLocalizedStoreItemName(model.Name, StoreLocalizationHelper.GetStoreLikeLocale());

                    ItemName = string.Format(isInstalled ? _localizer.GetLocalizedString("StoreSmallUpdateItemName") : _localizer.GetLocalizedString("StoreSmallInstallItemName"), _name);
                    ItemIcon = new BitmapImage(UIHelper.GetUriFromString(model.ReadyToUseIcon ?? LScriptLangHelper.ExecuteScript(model.Icon)));
                    DeveloperText = string.Format(_localizer.GetLocalizedString("StoreSmallDeveloperText"), model.Developer);
                    CategoryText = string.Format(_localizer.GetLocalizedString("Source"), packFile);
                    VersionText = string.Format(_localizer.GetLocalizedString("Version"), model.Version);

                    if (!string.IsNullOrEmpty(model.Color))
                        ItemColor = UIHelper.HexToSolidColorBrushConverter(model.Color);

                    SetLoadingMode(false);
                    IsDownloadProgressVisible = false;

                    if (DatabaseHelper.Instance.IsItemInstalled(_storeId))
                    {
                        string curV = DatabaseHelper.Instance.GetItemById(_storeId)?.CurrentVersion.NullIfEmpty() ?? "0.0.0.0";
                        string iV = model.Version;

                        if (!string.IsNullOrEmpty(curV) && !string.IsNullOrEmpty(iV))
                        {
                            if (VersionHelper.CompareVersionStrings(curV, iV) >= 0)
                            {
                                ShowError(_localizer.GetLocalizedString("ItemAlreadyInstalledWarn"));
                            }
                        }
                        else
                        {
                            ShowError(_localizer.GetLocalizedString("ItemAlreadyInstalledWarn"));
                        }


                    }

                }
            }
            catch (Exception ex)
            {
                ShowError($"{ex.Message}");
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    /// <summary>Installs the pack.</summary>
    public async Task InstallAsync()
    {
        if (!IsGetEnabled)
            return;
        IsGetEnabled = false;
        _isErrorHappens = false;
        ShowDownloadState();
        StatusText = _localizer.GetLocalizedString("GettingReady");
        await LocalItemsInstallerHelper.Instance.BeginLocalItemInstalling(
            _packFilePath, LocalItemsInstallerHelper_ErrorHappens, _installCancellation.Token);
    }

    /// <summary>Removes the item from the queue; called before the window closes.</summary>
    public void Cancel()
    {
        StoreHelper.Instance.RemoveItemFromQueue(_storeId);
    }

    /// <summary>Cancels the install and stops the queue handlers; called when the window is closed.</summary>
    public void Close()
    {
        _isClosed = true;
        _installCancellation.Cancel();
        DisconnectHandlers();
    }

    private void SetLoadingMode(bool isLoading)
    {
        IsGetEnabled = !isLoading;
        IsViewInStoreEnabled = !isLoading;
        IsLoading = isLoading;
    }

    private void ShowError(string message)
    {
        _isErrorHappens = true;
        SetLoadingMode(false);
        IsDescriptionVisible = false;
        IsDownloadProgressVisible = false;
        IsGetVisible = false;
        IsErrorVisible = true;

        ErrorCode = message;

        CancelButtonText = _localizer.GetLocalizedString("OkButtonText");
    }

    private void ShowDownloadState()
    {
        IsGetVisible = false;
        IsProgressIndeterminate = true;

        IsErrorVisible = false;
        IsDownloadProgressVisible = true;
    }

    private void ApplyStage(string state)
    {
        state ??= "QueueWaiting";
        string stageHeaderText = string.Empty;
        switch (state)
        {
            case "GETR":
                stageHeaderText = _localizer.GetLocalizedString("GettingReady");
                IsProgressIndeterminate = true;
                break;
            case "END":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                IsProgressIndeterminate = true;
                break;
            case "Downloading":
                stageHeaderText = _localizer.GetLocalizedString("Downloading");
                IsProgressIndeterminate = false;
                IsSpeedVisible = true;
                break;
            case "Extracting":
                stageHeaderText = _localizer.GetLocalizedString("Installing");
                IsProgressIndeterminate = true;
                break;
            case "ErrorHappens":
                stageHeaderText = _localizer.GetLocalizedString("ErrorHappens");
                break;
            case "Completed":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                IsProgressIndeterminate = true;
                break;
            case "CANC":
                stageHeaderText = _localizer.GetLocalizedString("Cancel");
                IsProgressIndeterminate = true;
                break;
            case "ConnectingToService":
                stageHeaderText = _localizer.GetLocalizedString("ConnectingToService");
                IsProgressIndeterminate = true;
                break;
            default:
                stageHeaderText = _localizer.GetLocalizedString(state);
                IsProgressIndeterminate = true;
                break;
        }
        StatusText = stageHeaderText;
    }

    private void ShowItemAfterInstallActions()
    {
        IsDownloadProgressVisible = false;
        CancelButtonText = _localizer.GetLocalizedString("OkButtonText");

        ItemName = string.Format(_localizer.GetLocalizedString("StoreSmallItemNameComplete"), _name);
    }

    private void LocalItemsInstallerHelper_ErrorHappens(string errorCode)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_isClosed)
                ShowError(errorCode);
        });
    }
}
