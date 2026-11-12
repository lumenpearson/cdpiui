#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// Owns designer models and synchronization. The host supplies editor access and UI notices.
internal sealed class ConfigMakerSimpleDesigner : ObservableObject
{
    private readonly IConfigMakerSimpleDesignerBackend _backend;
    private readonly Func<string> _currentComponent;
    private readonly Func<string> _getEditorText;
    private readonly Action<string> _setEditorText;
    private readonly ConfigMakerDesignerCommands _commands;
    private readonly Action<ConfigMakerDesignerNotice, Exception?> _notice;
    private bool _updatingDesigner;
    private bool _designerNeedsRefresh = true;
    private string _additionalDesignerArguments = string.Empty;

    public ConfigMakerSimpleDesigner(IConfigMakerSimpleDesignerBackend backend,
        Func<string> currentComponent, Func<string> getEditorText, Action<string> setEditorText,
        ConfigMakerDesignerCommands commands, Action<ConfigMakerDesignerNotice, Exception?> notice)
    {
        _backend = backend;
        _currentComponent = currentComponent;
        _getEditorText = getEditorText;
        _setEditorText = setEditorText;
        _commands = commands;
        _notice = notice;
    }

    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels { get; } = [];
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels { get; } = [];
    public bool IsSimpleDesignerSupported => _backend.IsSupported(_currentComponent());
    public string AdditionalDesignerArguments
    {
        get => _additionalDesignerArguments;
        private set => SetProperty(ref _additionalDesignerArguments, value ?? string.Empty);
    }

    public void MarkDirty() => _designerNeedsRefresh = true;

    public void UpdateAvailability()
    {
        OnPropertyChanged(nameof(IsSimpleDesignerSupported));
        if (!IsSimpleDesignerSupported)
        {
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
        }
    }

    public void OnAdditionalDesignerArgumentsChanged(string? text)
    {
        // The box already shows the text: do not send a change back to it.
        _additionalDesignerArguments = text ?? string.Empty;
        ApplyToCommand();
    }

    public void OnEditorViewChanged(bool leftDesigner, bool enteredDesigner)
    {
        if (leftDesigner)
        {
            ApplyToCommand();
        }
        if (enteredDesigner && _designerNeedsRefresh)
        {
            Load();
        }
    }

    private void Load()
    {
        if (!IsSimpleDesignerSupported)
        {
            return;
        }
        _updatingDesigner = true;
        try
        {
            AdditionalDesignerArguments = ComponentCommandLineFormatter.ToSingleLine(_getEditorText());
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            if (!_backend.TryLoad(_currentComponent(), DesignerSettingItemModels, DesignerExclusiveSettingItemModels))
            {
                _notice(ConfigMakerDesignerNotice.MissingAnnotation, null);
                return;
            }
            AttachCommands();
            AdditionalDesignerArguments = _backend.Parse(DesignerSettingItemModels,
                DesignerExclusiveSettingItemModels, ComponentCommandLineFormatter.ToSingleLine(_getEditorText()));
            _designerNeedsRefresh = false;
        }
        catch (Exception exception)
        {
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            _notice(ConfigMakerDesignerNotice.LoadFailed, exception);
        }
        finally
        {
            _updatingDesigner = false;
        }
    }

    private void AttachCommands()
    {
        foreach (GraphicDesignerSettingItemModel item in DesignerSettingItemModels)
        {
            item.DesignerTextValueChangedCommand = _commands.Text;
            item.DesignerBoolValueToggledCommand = _commands.Bool;
        }
        foreach (GraphicDesignerExclusiveSettingItemModel group in DesignerExclusiveSettingItemModels)
        {
            group.DesignerTextValueChangedCommand = _commands.Text;
            group.DesignerBoolValueToggledCommand = _commands.Bool;
            group.DesignerSelectedGuidChangedCommand = _commands.SelectedGuid;
        }
    }

    public void HandleDesignerTextValueChanged(Tuple<string, string> change)
    {
        GraphicDesignerSettingItemModel? item = FindItem(change.Item1);
        if (item != null)
        {
            item.Value = change.Item2;
            ApplyToCommand();
        }
    }

    public void HandleDesignerBoolValueToggled(Tuple<string, bool> change)
    {
        GraphicDesignerSettingItemModel? item = FindItem(change.Item1);
        if (item != null)
        {
            item.IsChecked = change.Item2;
            ApplyToCommand();
        }
    }

    public void HandleDesignerSelectedGuidChanged(Tuple<string, string> change)
    {
        GraphicDesignerExclusiveSettingItemModel? group = DesignerExclusiveSettingItemModels.FirstOrDefault(item =>
            string.Equals(item.Guid, change.Item1, StringComparison.Ordinal));
        if (group != null)
        {
            group.SelectedItemGuid = change.Item2;
            ApplyToCommand();
        }
    }

    private GraphicDesignerSettingItemModel? FindItem(string id) =>
        DesignerSettingItemModels.FirstOrDefault(item => string.Equals(item.Guid, id, StringComparison.Ordinal)) ??
        DesignerExclusiveSettingItemModels.SelectMany(group => group.Items)
            .FirstOrDefault(item => string.Equals(item.Guid, id, StringComparison.Ordinal));

    private void ApplyToCommand()
    {
        if (_updatingDesigner || _designerNeedsRefresh || !IsSimpleDesignerSupported)
        {
            return;
        }
        _updatingDesigner = true;
        try
        {
            string command = _backend.Build(DesignerSettingItemModels, DesignerExclusiveSettingItemModels,
                _additionalDesignerArguments);
            _setEditorText(ComponentCommandLineFormatter.FormatByFlags(command.Trim()));
            _designerNeedsRefresh = false;
        }
        finally
        {
            _updatingDesigner = false;
        }
    }
}
