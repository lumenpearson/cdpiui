using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CDPIUI.ConditionalLaunch;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.Shared.Extensions;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.ConditionalLaunch;

/// <summary>
/// State of the dialog that adds or edits a conditional action: the action type, the
/// parameter values, the preset choices and the validation that builds the result.
/// </summary>
public sealed partial class ConditionalActionEditorViewModel : ObservableObject
{
    private const string LogSource = "ConditionalActionContentDialog";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly List<ConditionalActionDefinition> _definitions;
    private readonly List<ConditionalParameterChoice> _componentChoices = [];
    private readonly List<ConditionalParameterChoice> _configKitChoices = [];
    private ConditionalAction? _existingAction;
    private ConditionalActionDefinition? _selectedDefinition;
    private bool _loading;
    private bool _isPresetEditor;
    private IReadOnlyList<ConditionalActionParameterViewModel> _parameters = [];
    private ConditionalParameterChoice? _selectedPresetComponent;
    private IReadOnlyList<ConditionalPresetChoice> _presetChoices = [];
    private ConditionalPresetChoice? _selectedPreset;
    private string _errorMessage = string.Empty;
    private bool _isErrorOpen;

    public ConditionalActionEditorViewModel(ConditionalAction? action)
    {
        _definitions = ConditionalLaunchUiCatalog.CreateActionDefinitions(_localizer);
        LoadInstalledComponents(action?.GetParameter("componentId"));
        LoadInstalledConfigKits(action?.GetParameter("kitId"));

        Title = Text(action == null ? "CL_AddActionDialogTitle" : "CL_EditActionDialogTitle");
        PrimaryButtonText = Text("CL_OkButtonText");
        CloseButtonText = Text("CL_CancelButtonText");

        _existingAction = action;
        if (action != null)
            InitialDefinition = _definitions.FirstOrDefault(item => item.Type == action.Type) ?? _definitions.FirstOrDefault();
    }

    public string Title { get; }

    public string PrimaryButtonText { get; }

    public string CloseButtonText { get; }

    internal IReadOnlyList<ConditionalActionDefinition> Definitions => _definitions;

    /// <summary>The action type selected when the dialog opens; null for a new action.</summary>
    internal ConditionalActionDefinition? InitialDefinition { get; }

    public ConditionalAction? ResultAction { get; private set; }

    /// <summary>True when the selected type is Apply preset, which has its own editor.</summary>
    public bool IsPresetEditor
    {
        get => _isPresetEditor;
        private set => SetProperty(ref _isPresetEditor, value);
    }

    /// <summary>
    /// The parameters of the selected action type. A change of the action type raises a change
    /// of this property, also when the Apply preset editor replaces an empty list.
    /// </summary>
    public IReadOnlyList<ConditionalActionParameterViewModel> Parameters => _parameters;

    public string PresetComponentHeader => Text("CL_ParameterComponent");

    public string PresetHeader => Text("CL_ParameterPreset");

    internal IReadOnlyList<ConditionalParameterChoice> PresetComponentChoices => _componentChoices;

    /// <summary>The component of the Apply preset editor. Changing it reloads the presets.</summary>
    internal ConditionalParameterChoice? SelectedPresetComponent
    {
        get => _selectedPresetComponent;
        set
        {
            if (SetProperty(ref _selectedPresetComponent, value))
                LoadPresetChoices(null, null);
        }
    }

    internal IReadOnlyList<ConditionalPresetChoice> PresetChoices
    {
        get => _presetChoices;
        private set => SetProperty(ref _presetChoices, value);
    }

    internal ConditionalPresetChoice? SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsErrorOpen
    {
        get => _isErrorOpen;
        private set => SetProperty(ref _isErrorOpen, value);
    }

    /// <summary>Called when the action type selection changes.</summary>
    internal void SelectDefinition(object? selectedItem)
    {
        _selectedDefinition = selectedItem as ConditionalActionDefinition;
        if (_loading || _selectedDefinition == null)
            return;

        var existing = _existingAction?.Type == _selectedDefinition.Type ? _existingAction : null;
        BuildParameterEditor(_selectedDefinition, existing);
        _existingAction = null;
    }

    /// <summary>
    /// Builds <see cref="ResultAction"/> from the entered values. Returns false and shows the
    /// error when a value is missing.
    /// </summary>
    public bool TryCreateAction()
    {
        try
        {
            ResultAction = CreateAction();
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsErrorOpen = true;
            return false;
        }
    }

    private void LoadInstalledComponents(string? existingComponentId)
    {
        ObservableCollection<ViewComponentModel> components = [];
        UIHelper.LoadInstalledComponentsList(components);
        _componentChoices.AddRange(components.Select(component =>
            new ConditionalParameterChoice(component.DisplayName, component.StoreId)));

        if (!string.IsNullOrWhiteSpace(existingComponentId) &&
            !_componentChoices.Any(item => string.Equals(
                item.Value, existingComponentId, StringComparison.OrdinalIgnoreCase)))
        {
            _componentChoices.Add(new ConditionalParameterChoice(existingComponentId, existingComponentId));
        }
    }

    private void LoadInstalledConfigKits(string? existingKitId)
    {
        try
        {
            _configKitChoices.AddRange(DatabaseHelper.Instance
                .GetItemsByType("configlist")
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .Select(item => new ConditionalParameterChoice(
                    item.ShortName.NullIfEmpty() ?? item.Id!,
                    item.Id!)));
        }
        catch (Exception ex)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(LogSource, $"Cannot load installed config kits: {ex.Message}");
        }

        if (!string.IsNullOrWhiteSpace(existingKitId) &&
            !_configKitChoices.Any(item => string.Equals(
                item.Value, existingKitId, StringComparison.OrdinalIgnoreCase)))
        {
            _configKitChoices.Add(new ConditionalParameterChoice(
                ResolveStoreItemName(existingKitId),
                existingKitId));
        }
    }

    private void BuildParameterEditor(
        ConditionalActionDefinition definition,
        ConditionalAction? existingAction)
    {
        _loading = true;

        if (definition.Type == ConditionalActionType.ApplyPreset)
        {
            BuildPresetEditor(existingAction);
            IsPresetEditor = true;
            SetParameters([]);
            _loading = false;
            return;
        }

        List<ConditionalActionParameterViewModel> parameters = [];
        foreach (var parameter in definition.Parameters)
            parameters.Add(CreateParameter(parameter, existingAction?.GetParameter(parameter.Name)));

        UpdateParameterVisibility(parameters);
        IsPresetEditor = false;
        SetParameters(parameters);
        _loading = false;
    }

    private void SetParameters(IReadOnlyList<ConditionalActionParameterViewModel> parameters)
    {
        _parameters = parameters;
        OnPropertyChanged(nameof(Parameters));
    }

    private ConditionalActionParameterViewModel CreateParameter(
        ConditionalParameterDefinition parameter,
        string? existingValue)
    {
        IReadOnlyList<ConditionalParameterChoice> choices = parameter.UseInstalledComponentSelector
            ? _componentChoices
            : parameter.UseInstalledConfigKitSelector
                ? _configKitChoices
                : parameter.Choices;

        if (choices.Count > 0 || parameter.UseInstalledComponentSelector ||
            parameter.UseInstalledConfigKitSelector)
        {
            ConditionalActionParameterViewModel choice = new(
                parameter, ConditionalActionParameterKind.Choice, choices, Parameter_ValueChanged);
            choice.SelectedChoice = choices.FirstOrDefault(item =>
                string.Equals(item.Value, existingValue, StringComparison.OrdinalIgnoreCase))
                ?? choices.FirstOrDefault();
            return choice;
        }

        if (parameter.IsNumber)
        {
            ConditionalActionParameterViewModel number = new(
                parameter, ConditionalActionParameterKind.Number, [], Parameter_ValueChanged);
            number.Number = double.TryParse(existingValue ?? parameter.DefaultValue, out var value)
                ? value
                : parameter.Minimum;
            return number;
        }

        ConditionalActionParameterViewModel text = new(
            parameter, ConditionalActionParameterKind.Text, [], Parameter_ValueChanged);
        text.Text = existingValue ?? parameter.DefaultValue ?? string.Empty;
        return text;
    }

    private void Parameter_ValueChanged(ConditionalActionParameterViewModel parameter)
    {
        if (!_loading && parameter.Name == "target")
            UpdateParameterVisibility(_parameters);
    }

    private void BuildPresetEditor(ConditionalAction? existingAction)
    {
        var componentId = existingAction?.GetParameter("componentId");
        _selectedPresetComponent = _componentChoices.FirstOrDefault(choice =>
            string.Equals(choice.Value, componentId, StringComparison.OrdinalIgnoreCase))
            ?? _componentChoices.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPresetComponent));

        LoadPresetChoices(
            existingAction?.GetParameter("packId"),
            existingAction?.GetParameter("fileName"));
    }

    private static List<ConditionalPresetChoice> GetPresetChoices(string componentId)
    {
        try
        {
            ComponentItemsLoaderHelper.Instance.Init();
            var componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId);
            var configuration = componentHelper?.GetConfigHelper();
            if (configuration == null)
                return [];

            return configuration.GetConfigItems()
                .Where(item => item != null && !item.MarkAsRemoved)
                .Select(item => new ConditionalPresetChoice(
                    item.name ?? item.not_converted_name ?? item.file_name ?? item.packId ?? string.Empty,
                    ResolveStoreItemName(item.packId),
                    item.packId ?? string.Empty,
                    item.file_name ?? string.Empty))
                .Where(item => !string.IsNullOrWhiteSpace(item.FileName))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void LoadPresetChoices(string? selectedPackId, string? selectedFileName)
    {
        var componentId = _selectedPresetComponent?.Value;
        var presets = string.IsNullOrWhiteSpace(componentId)
            ? []
            : GetPresetChoices(componentId);

        if (!string.IsNullOrWhiteSpace(selectedFileName) &&
            !presets.Any(item =>
                string.Equals(item.PackId, selectedPackId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.FileName, selectedFileName, StringComparison.OrdinalIgnoreCase)))
        {
            presets.Add(new ConditionalPresetChoice(
                selectedFileName,
                ResolveStoreItemName(selectedPackId),
                selectedPackId ?? string.Empty,
                selectedFileName));
        }

        _selectedPreset = presets.FirstOrDefault(item =>
            string.Equals(item.PackId, selectedPackId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.FileName, selectedFileName, StringComparison.OrdinalIgnoreCase))
            ?? presets.FirstOrDefault();
        PresetChoices = presets;
    }

    private static void UpdateParameterVisibility(IReadOnlyList<ConditionalActionParameterViewModel> parameters)
    {
        var values = ReadParameterValues(parameters);
        foreach (var parameter in parameters)
            parameter.IsVisible = parameter.Definition.IsVisible(values);
    }

    private static Dictionary<string, string> ReadParameterValues(
        IReadOnlyList<ConditionalActionParameterViewModel> parameters)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
            values[parameter.Name] = parameter.Value;
        return values;
    }

    private ConditionalAction CreateAction()
    {
        if (_selectedDefinition is not ConditionalActionDefinition definition)
            throw new InvalidDataException(Text("CL_ChooseAction"));

        ConditionalAction action = new() { Type = definition.Type };
        if (definition.Type == ConditionalActionType.ApplyPreset)
        {
            var componentId = _selectedPresetComponent;
            var preset = _selectedPreset;
            if (componentId == null)
                throw new InvalidDataException(Text("CL_ErrorComponentRequired"));
            if (preset == null)
                throw new InvalidDataException(Text("CL_ErrorPresetRequired"));

            action.SetParameter("componentId", componentId.Value);
            action.SetParameter("packId", preset.PackId);
            action.SetParameter("fileName", preset.FileName);
            return action;
        }

        var values = ReadParameterValues(_parameters);
        foreach (var parameter in definition.Parameters)
        {
            if (!parameter.IsVisible(values))
                continue;
            if (parameter.IsRequired(values) &&
                (!values.TryGetValue(parameter.Name, out var value) || string.IsNullOrWhiteSpace(value)))
            {
                throw new InvalidDataException(string.Format(
                    Text("CL_ErrorParameterRequiredFormat"), parameter.Label));
            }
        }

        foreach (var parameter in definition.Parameters.Where(parameter => parameter.IsVisible(values)))
        {
            if (values.TryGetValue(parameter.Name, out var value) && !string.IsNullOrWhiteSpace(value))
                action.SetParameter(parameter.Name, value);
        }
        return action;
    }

    private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);

    private static string ResolveStoreItemName(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;
        try
        {
            return DatabaseHelper.Instance.GetItemById(id)?.ShortName.NullIfEmpty() ?? id;
        }
        catch
        {
            return id;
        }
    }
}
