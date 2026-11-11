using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.System;
using CDPIUI.Helper.CreateConfigHelper;
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
    private readonly CreateConfigConditionDraft _conditionDraft;
    private readonly CreateConfigTestSession _testSession;
    private readonly CreateConfigGraphicDesigner _graphicDesigner;
    private readonly CreateConfigComponentSelection _componentSelection;

    private ConfigItem _configItem;
    private string _listDirectory = string.Empty;
    private string _binDirectory = string.Empty;

    private string _pageTitle;
    private string _saveButtonText;
    private bool _isComponentChoiceEnabled = true;
    private bool _isTextEditorVisible;
    private bool _isDesignerVisible;
    private bool _isVariablesVisible;
    private bool _isListsDirectoryButtonVisible;
    private bool _isBinsDirectoryButtonVisible;

    public CreateNewConfigViewModel(ICreateNewConfigView view)
    {
        _view = view;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _componentSelection = new CreateConfigComponentSelection(
            id => DesignerSupportedComponentIds.Contains(id),
            () => _view.SelectDesignerTab(), () => _view.SelectTextEditorTab(),
            HandleSelectionChange, () => _view.SetExitAskAvailable(true),
            AuditSaveAvailable, () => _view.EditorText);
        _componentSelection.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _graphicDesigner = new CreateConfigGraphicDesigner(DesignerSettingItemModels, DesignerExclusiveSettingItemModels,
            new CreateConfigGraphicDesignerBackend(), component => HardcodedItemIds.ComponentIds[component]);
        _graphicDesigner.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _testSession = new CreateConfigTestSession(new CreateConfigComponentTestTasks(),
            () => SelectedComponent is ConfigComponentOption component
                ? (true, component.Id) : (false, string.Empty),
            ConvertDesignerLikeSettingsToString,
            () => CreateConfig(0, saveLocalization: false),
            config => ConfigurationService.GetStartupParametersByConfigItem(config),
            key => _localizer.GetLocalizedString(key),
            action => _dispatcherQueue?.TryEnqueue(() => action()));
        _testSession.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _conditionDraft = new CreateConfigConditionDraft(Conditions,
            name => CreateConfigPageHelper.IsNameCorrect(name),
            (name, on, off) => LScriptLangHelper.CreateCondition(name, on, off),
            key => _localizer.GetLocalizedString(key),
            () => _view.SetExitAskAvailable(true));
        _conditionDraft.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);

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
        _testSession.InitializeText(_localizer.GetLocalizedString("TestThis"));
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
        get => _componentSelection.SelectedComponent;
        set => _componentSelection.SelectedComponent = value;
    }

    public string DisplayName
    {
        get => _componentSelection.DisplayName;
        set => _componentSelection.DisplayName = value;
    }

    public string AdditionalSettings
    {
        get => _graphicDesigner.AdditionalSettings;
        set => _graphicDesigner.AdditionalSettings = value;
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
        get => _componentSelection.IsAdditionalSettingsVisible;
        private set => _componentSelection.IsAdditionalSettingsVisible = value;
    }

    public bool IsNotReadyErrorVisible
    {
        get => _componentSelection.IsNotReadyErrorVisible;
        private set => _componentSelection.IsNotReadyErrorVisible = value;
    }

    public bool IsTabBarVisible
    {
        get => _componentSelection.IsTabBarVisible;
        private set => _componentSelection.IsTabBarVisible = value;
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
    public bool IsDesignerSupported => _componentSelection.IsDesignerSupported;

    /// <summary>Call when the user or the code changed the text of the editor.</summary>
    public void OnEditorTextChanged()
    {
        _view.SetExitAskAvailable(true);
        AuditSaveAvailable();
    }

    private bool IsSaveAvailable() => _componentSelection.IsSaveAvailable;

    private void AuditSaveAvailable()
    {
        _saveCommand?.NotifyCanExecuteChanged();
        _testCommand?.NotifyCanExecuteChanged();
    }

    private void CheckViewType() => _componentSelection.CheckViewType();

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
