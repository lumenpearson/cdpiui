using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.System;
using CDPIUI.Helper.LScript;
using CDPIUI.Shared;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>
/// The state of the config editor page: the target component, the name, the variables and
/// conditions, the graphic designer, saving and the test run.
/// </summary>
public sealed partial class CreateNewConfigViewModel : ObservableObject
{
    private const string LogSource = "CreateNewConfigPage";

    private static readonly List<string> DesignerSupportedComponentIds = ["CSSIXC048", "CSGIVS036", "CSNIG9025"];

    private readonly ICreateNewConfigView _view;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _testCommand;

    private ConfigItem _configItem;
    private string _listDirectory = string.Empty;
    private string _binDirectory = string.Empty;

    private ConfigComponentOption _selectedComponent;
    private string _displayName = string.Empty;
    private string _pageTitle;
    private string _saveButtonText;
    private bool _isComponentChoiceEnabled = true;
    private bool _isAdditionalSettingsVisible;
    private bool _isNotReadyErrorVisible = true;
    private bool _isTabBarVisible = true;
    private bool _isTextEditorVisible;
    private bool _isDesignerVisible;
    private bool _isVariablesVisible;
    private bool _isListsDirectoryButtonVisible;
    private bool _isBinsDirectoryButtonVisible;
    private string _additionalSettings = string.Empty;

    public CreateNewConfigViewModel(ICreateNewConfigView view)
    {
        _view = view;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        RemoveConditionCommand = new RelayCommand(p => RemoveCondition(p));
        RemoveVariableCommand = new RelayCommand(p => RemoveVariable(p));
        VariableChangedCommand = new RelayCommand(p => VariableChangedValue((Tuple<string, string>)p));

        GraphicDesignerTextValueChangedCommand = new RelayCommand(p => HandleGraphicDesignerTextValueChanged((Tuple<string, string>)p));
        GraphicDesignerBoolValueToggledCommand = new RelayCommand(p => HandleGraphicDesignerBoolValueToggled((Tuple<string, bool>)p));
        GraphicDesignerSelectedGuidChangedCommand = new RelayCommand(p => HandleGraphicDesignerSelectedGuidChanged((Tuple<string, string>)p));

        _saveCommand = new AsyncRelayCommand(SaveAsync, IsSaveAvailable);
        _testCommand = new AsyncRelayCommand(ToggleTestRunAsync, IsSaveAvailable);
        OpenListsDirectoryCommand = new RelayCommand(OpenListsDirectory);
        OpenBinsDirectoryCommand = new RelayCommand(OpenBinsDirectory);

        ComponentOptions = HardcodedItemIds.ComponentIds
            .Select(component => new ConfigComponentOption { Id = component.Value, Name = component.Key.ToString() })
            .ToList();

        _pageTitle = _localizer.GetLocalizedString("CreateNewConfig");
        _saveButtonText = _localizer.GetLocalizedString("Save");
        _testButtonText = _localizer.GetLocalizedString("TestThis");
    }

    public ObservableCollection<ConditionVariableModel> Conditions { get; } = [];
    public ObservableCollection<VariableModel> Variables { get; } = [];
    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels { get; } = [];
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels { get; } = [];

    public IReadOnlyList<ConfigComponentOption> ComponentOptions { get; }

    public ICommand RemoveConditionCommand { get; }
    public ICommand RemoveVariableCommand { get; }
    public ICommand VariableChangedCommand { get; }
    public ICommand GraphicDesignerTextValueChangedCommand { get; }
    public ICommand GraphicDesignerBoolValueToggledCommand { get; }
    public ICommand GraphicDesignerSelectedGuidChangedCommand { get; }
    public ICommand SaveCommand => _saveCommand;
    public ICommand TestCommand => _testCommand;
    public ICommand OpenListsDirectoryCommand { get; }
    public ICommand OpenBinsDirectoryCommand { get; }

    /// <summary>The pack id the conditions are localized for.</summary>
    public string EditItemId { get; private set; } = string.Empty;

    public PageOpenModes PageOpenMode { get; private set; } = PageOpenModes.None;

    public ConfigComponentOption SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (!SetProperty(ref _selectedComponent, value))
                return;

            CheckViewType();
            _view.SetExitAskAvailable(true);
            AuditSaveAvailable();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value ?? string.Empty))
                return;

            _view.SetExitAskAvailable(true);
            AuditSaveAvailable();
        }
    }

    public string AdditionalSettings
    {
        get => _additionalSettings;
        set => SetProperty(ref _additionalSettings, value ?? string.Empty);
    }

    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    public string SaveButtonText
    {
        get => _saveButtonText;
        private set => SetProperty(ref _saveButtonText, value);
    }

    public bool IsComponentChoiceEnabled
    {
        get => _isComponentChoiceEnabled;
        private set => SetProperty(ref _isComponentChoiceEnabled, value);
    }

    public bool IsAdditionalSettingsVisible
    {
        get => _isAdditionalSettingsVisible;
        private set => SetProperty(ref _isAdditionalSettingsVisible, value);
    }

    public bool IsNotReadyErrorVisible
    {
        get => _isNotReadyErrorVisible;
        private set => SetProperty(ref _isNotReadyErrorVisible, value);
    }

    public bool IsTabBarVisible
    {
        get => _isTabBarVisible;
        private set => SetProperty(ref _isTabBarVisible, value);
    }

    public bool IsTextEditorVisible
    {
        get => _isTextEditorVisible;
        private set => SetProperty(ref _isTextEditorVisible, value);
    }

    public bool IsDesignerVisible
    {
        get => _isDesignerVisible;
        private set => SetProperty(ref _isDesignerVisible, value);
    }

    public bool IsVariablesVisible
    {
        get => _isVariablesVisible;
        private set => SetProperty(ref _isVariablesVisible, value);
    }

    public bool IsListsDirectoryButtonVisible
    {
        get => _isListsDirectoryButtonVisible;
        private set => SetProperty(ref _isListsDirectoryButtonVisible, value);
    }

    public bool IsBinsDirectoryButtonVisible
    {
        get => _isBinsDirectoryButtonVisible;
        private set => SetProperty(ref _isBinsDirectoryButtonVisible, value);
    }

    /// <summary>The selected component has a graphic designer.</summary>
    public bool IsDesignerSupported => DesignerSupportedComponentIds.Contains(SelectedComponent?.Id ?? "");

    /// <summary>Call when the user or the code changed the text of the editor.</summary>
    public void OnEditorTextChanged()
    {
        _view.SetExitAskAvailable(true);
        AuditSaveAvailable();
    }

    private bool IsSaveAvailable() =>
        SelectedComponent != null &&
        !string.IsNullOrEmpty(DisplayName) &&
        !string.IsNullOrEmpty(_view.EditorText);

    private void AuditSaveAvailable()
    {
        _saveCommand?.NotifyCanExecuteChanged();
        _testCommand?.NotifyCanExecuteChanged();
    }

    private void CheckViewType()
    {
        if (SelectedComponent == null)
        {
            IsAdditionalSettingsVisible = false;
            IsNotReadyErrorVisible = true;
            return;
        }

        IsAdditionalSettingsVisible = true;
        IsNotReadyErrorVisible = false;

        if (IsDesignerSupported)
        {
            _view.SelectDesignerTab();
            IsTabBarVisible = true;
            HandleSelectionChange(false);
        }
        else
        {
            HandleSelectionChange(true);
            _view.SelectTextEditorTab();
            IsTabBarVisible = false;
        }
    }

    private void CheckFolderOpenButtonsVisibility()
    {
        IsBinsDirectoryButtonVisible = false;
        IsListsDirectoryButtonVisible = false;
        if (!string.IsNullOrEmpty(_listDirectory) && _configItem != null)
        {
            IsListsDirectoryButtonVisible = true;
        }
        if (!string.IsNullOrEmpty(_binDirectory) && _configItem != null)
        {
            IsBinsDirectoryButtonVisible = true;
        }
    }

    private void OpenListsDirectory()
    {
        if (!string.IsNullOrEmpty(_listDirectory) && _configItem != null)
        {
            ShellHelper.LookupDirectory(LScriptLangHelper.ExecuteScript(_listDirectory, callItemId: SharedConstants.LocalUserItemsId));
        }
    }

    private void OpenBinsDirectory()
    {
        if (!string.IsNullOrEmpty(_binDirectory) && _configItem != null)
        {
            ShellHelper.LookupDirectory(LScriptLangHelper.ExecuteScript(_binDirectory, callItemId: SharedConstants.LocalUserItemsId));
        }
    }
}
