using System.Threading.Tasks;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.ViewModels.ConfigMaker;

// The preset variables list.
public sealed partial class ConfigMakerViewModel
{
    public AsyncRelayCommand CreateVariableCommand { get; }

    public Task EditVariableAsync(ConfigMakerVariableDefinition variable) =>
        RunReportingErrorsAsync(() => _variableActions.EditVariableAsync(variable));

    public Task RemoveVariableAsync(ConfigMakerVariableDefinition variable) =>
        RunReportingErrorsAsync(() => _variableActions.RemoveVariableAsync(variable));

    /// <summary>Inserts the reference of a variable at the editor cursor.</summary>
    public void InsertVariableReference(ConfigMakerVariableDefinition variable) =>
        _variableActions.InsertVariableReference(variable);

    private Task CreateVariableAsync() => _variableActions.CreateVariableAsync();

#nullable enable annotations
    string IConfigMakerVariableActionsHost.CommandText => CommandText;
    bool IConfigMakerVariableActionsHost.IsEditorReadOnly => IsEditorReadOnly;
    Task<ConfigMakerVariableEditResult?> IConfigMakerVariableActionsHost.EditVariableAsync(
        ConfigMakerPresetDocument document, ConfigMakerVariableDefinition? variable) => _view.EditVariableAsync(document, variable);
    Task<bool> IConfigMakerVariableActionsHost.ConfirmRemoveReferencedVariableAsync() => _view.ConfirmAsync(
        _localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogTitle"),
        _localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogMessage"),
        _localizer.GetLocalizedString("Remove"));
    void IConfigMakerVariableActionsHost.SetEditorText(string text) => SetEditorText(text);
    void IConfigMakerVariableActionsHost.InsertAtCursor(string reference) => InsertAtCursor(reference);
    void IConfigMakerVariableActionsHost.AttachExistingPresetFiles() => AttachExistingPresetFiles();
    void IConfigMakerVariableActionsHost.ShowVariablesTool() => _view.ShowVariablesTool();
    void IConfigMakerVariableActionsHost.RebuildPresetStructure() => RebuildPresetStructure();
    void IConfigMakerVariableActionsHost.ScheduleDiagnostics() => ScheduleDiagnostics();
}
