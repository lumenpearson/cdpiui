#nullable enable

using System.Threading.Tasks;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerVariableActionsHost
{
    Task<ConfigMakerVariableEditResult?> EditVariableAsync(ConfigMakerPresetDocument document, ConfigMakerVariableDefinition? variable);
    Task<bool> ConfirmRemoveReferencedVariableAsync();
    string CommandText { get; }
    bool IsEditorReadOnly { get; }
    void SetEditorText(string text);
    void InsertAtCursor(string reference);
    void AttachExistingPresetFiles();
    void ShowVariablesTool();
    void RebuildPresetStructure();
    void ScheduleDiagnostics();
}
