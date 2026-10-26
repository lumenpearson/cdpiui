using System.Windows.Input;
using CDPIUI.Core;
using CDPIUI.Helper;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>
/// The state of the config helper window: the status bar, the menu items that depend on
/// the current page, the help item and the editor background setting.
/// </summary>
public sealed partial class CreateConfigHelperWindowViewModel : ObservableObject
{
    private const string BackgroundSettingsGroup = "APPEARANCE";
    private const string BackgroundSettingsKey = "configEditorBackground";

    private readonly ILocalizer _localizer = Localizer.Get();

    private bool _isWorking;
    private string _statusText = string.Empty;
    private bool _isHomeEnabled = true;
    private bool _isCreateNewConfigEnabled = true;
    private bool _isEditorMenuVisible;
    private bool _isBackButtonVisible = true;
    private string _currentHelpText = string.Empty;

    public CreateConfigHelperWindowViewModel(string offlineHelpUri)
    {
        string background = SettingsManager.Instance.GetValue<string>(BackgroundSettingsGroup, BackgroundSettingsKey);
        EditorBackground = background switch
        {
            "Mica" or "MicaTransparent" or "MicaSmoke" => background,
            _ => "Black",
        };

        ReportIssueCommand = new RelayCommand(() => _ = UrlOpenHelper.LaunchReportUrl());
        OnlineHelpCommand = new RelayCommand(() => _ = UrlOpenHelper.LaunchWikiUrl());
        OfflineHelpCommand = new RelayCommand(() => Commands.CommandsHandler.HandleCommand(offlineHelpUri));
    }

    public ICommand ReportIssueCommand { get; }
    public ICommand OnlineHelpCommand { get; }
    public ICommand OfflineHelpCommand { get; }

    /// <summary>Leaving the current page asks the user first.</summary>
    public bool IsOperationExitAskAvailable { get; set; }

    /// <summary>The key of the editor background: Black, Mica, MicaTransparent or MicaSmoke.</summary>
    public string EditorBackground { get; private set; }

    public bool IsDefaultBackground => EditorBackground == "Black";
    public bool IsMicaBackground => EditorBackground == "Mica";
    public bool IsMicaAltBackground => EditorBackground == "MicaTransparent";
    public bool IsMicaSmokeBackground => EditorBackground == "MicaSmoke";

    public bool IsWorking
    {
        get => _isWorking;
        private set
        {
            if (SetProperty(ref _isWorking, value))
                OnPropertyChanged(nameof(IsReady));
        }
    }

    public bool IsReady => !IsWorking;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsHomeEnabled
    {
        get => _isHomeEnabled;
        private set => SetProperty(ref _isHomeEnabled, value);
    }

    public bool IsCreateNewConfigEnabled
    {
        get => _isCreateNewConfigEnabled;
        private set => SetProperty(ref _isCreateNewConfigEnabled, value);
    }

    /// <summary>The Edit and View menus, which only the config editor uses.</summary>
    public bool IsEditorMenuVisible
    {
        get => _isEditorMenuVisible;
        private set => SetProperty(ref _isEditorMenuVisible, value);
    }

    public bool IsBackButtonVisible
    {
        get => _isBackButtonVisible;
        set => SetProperty(ref _isBackButtonVisible, value);
    }

    public string CurrentHelpText
    {
        get => _currentHelpText;
        private set => SetProperty(ref _currentHelpText, value);
    }

    /// <summary>Shows the status in the status bar. Call on the UI thread.</summary>
    public void SetStatus(bool isWorking, string text)
    {
        IsWorking = isWorking;
        StatusText = string.IsNullOrEmpty(text) ? _localizer.GetLocalizedString("Ready") : text;
    }

    /// <summary>Enables the menu items for the page the window navigates to.</summary>
    public void AuditMenuItems(bool isMainPage, bool isConfigEditorPage)
    {
        IsHomeEnabled = true;
        IsCreateNewConfigEnabled = true;
        IsEditorMenuVisible = false;

        if (isMainPage)
        {
            IsHomeEnabled = false;
        }
        else if (isConfigEditorPage)
        {
            IsCreateNewConfigEnabled = false;
            IsEditorMenuVisible = true;
        }
    }

    public void UpdateCurrentHelpItem(string pageName)
    {
        CurrentHelpText = string.Format(_localizer.GetLocalizedString("/Flashlight/GetHelpFor"), _localizer.GetLocalizedString($"/Flashlight/{pageName}"));
    }

    /// <summary>Saves the editor background the user picked.</summary>
    public void SetEditorBackground(string key)
    {
        EditorBackground = key;
        SettingsManager.Instance.SetValue(BackgroundSettingsGroup, BackgroundSettingsKey, key);
    }
}
