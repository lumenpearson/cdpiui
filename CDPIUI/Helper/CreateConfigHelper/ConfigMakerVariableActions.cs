#nullable enable

using System;
using System.Threading.Tasks;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Helper.CreateConfigHelper;

internal sealed class ConfigMakerVariableActions
{
    private readonly ConfigMakerPresetDocument _document;
    private readonly IConfigMakerVariableActionsHost _host;

    public ConfigMakerVariableActions(ConfigMakerPresetDocument document, IConfigMakerVariableActionsHost host)
    {
        _document = document;
        _host = host;
    }

    public async Task EditVariableAsync(ConfigMakerVariableDefinition? variable)
    {
        if (variable == null)
        {
            return;
        }
        ConfigMakerVariableEditResult? result = await _host.EditVariableAsync(_document, variable);
        if (result == null)
        {
            return;
        }
        int index = _document.Variables.IndexOf(variable);
        if (index < 0)
        {
            return;
        }
        if (!string.Equals(result.OriginalName, result.Variable.Name, StringComparison.Ordinal))
        {
            _document.ReplaceVariableReference(result.OriginalName, result.Variable.Name);
            _host.SetEditorText(_document.CommandText);
        }
        _document.Variables[index] = result.Variable;
        _host.AttachExistingPresetFiles();
        _host.RebuildPresetStructure();
        _host.ScheduleDiagnostics();
    }

    public async Task RemoveVariableAsync(ConfigMakerVariableDefinition? variable)
    {
        if (variable == null)
        {
            return;
        }
        bool referenced = _host.CommandText.Contains(variable.Reference, StringComparison.OrdinalIgnoreCase);
        if (referenced && !await _host.ConfirmRemoveReferencedVariableAsync())
        {
            return;
        }
        _document.Variables.Remove(variable);
        _host.RebuildPresetStructure();
        _host.ScheduleDiagnostics();
    }

    public void InsertVariableReference(ConfigMakerVariableDefinition? variable)
    {
        if (!_host.IsEditorReadOnly && variable != null)
        {
            _host.InsertAtCursor(variable.Reference);
        }
    }

    public async Task CreateVariableAsync()
    {
        ConfigMakerVariableEditResult? result = await _host.EditVariableAsync(_document, null);
        if (result == null)
        {
            return;
        }
        _document.Variables.Add(result.Variable);
        _host.AttachExistingPresetFiles();
        _host.ShowVariablesTool();
        _host.RebuildPresetStructure();
        _host.ScheduleDiagnostics();
    }
}
