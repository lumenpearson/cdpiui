using System;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels.ConfigMaker;

// The preset variables list.
public sealed partial class ConfigMakerViewModel
{
    public AsyncRelayCommand CreateVariableCommand { get; }

    public Task EditVariableAsync(ConfigMakerVariableDefinition variable) =>
        RunReportingErrorsAsync(async () =>
        {
            if (variable == null)
            {
                return;
            }
            ConfigMakerVariableEditResult result = await _view.EditVariableAsync(_presetDocument, variable);
            if (result == null)
            {
                return;
            }
            int index = _presetDocument.Variables.IndexOf(variable);
            if (index < 0)
            {
                return;
            }
            if (!string.Equals(result.OriginalName, result.Variable.Name, StringComparison.Ordinal))
            {
                _presetDocument.ReplaceVariableReference(
                    result.OriginalName,
                    result.Variable.Name);
                SetEditorText(_presetDocument.CommandText);
            }
            _presetDocument.Variables[index] = result.Variable;
            AttachExistingPresetFiles();
            RebuildPresetStructure();
            ScheduleDiagnostics();
        });

    public Task RemoveVariableAsync(ConfigMakerVariableDefinition variable) =>
        RunReportingErrorsAsync(async () =>
        {
            if (variable == null)
            {
                return;
            }
            bool referenced = CommandText.Contains(variable.Reference, StringComparison.OrdinalIgnoreCase);
            if (referenced &&
                !await _view.ConfirmAsync(
                    _localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogTitle"),
                    _localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogMessage"),
                    _localizer.GetLocalizedString("Remove")))
            {
                return;
            }
            _presetDocument.Variables.Remove(variable);
            RebuildPresetStructure();
            ScheduleDiagnostics();
        });

    /// <summary>Inserts the reference of a variable at the editor cursor.</summary>
    public void InsertVariableReference(ConfigMakerVariableDefinition variable)
    {
        if (!IsEditorReadOnly && variable != null)
        {
            InsertAtCursor(variable.Reference);
        }
    }

    private async Task CreateVariableAsync()
    {
        ConfigMakerVariableEditResult result = await _view.EditVariableAsync(_presetDocument, null);
        if (result == null)
        {
            return;
        }
        _presetDocument.Variables.Add(result.Variable);
        AttachExistingPresetFiles();
        _view.ShowVariablesTool();
        RebuildPresetStructure();
        ScheduleDiagnostics();
    }
}
