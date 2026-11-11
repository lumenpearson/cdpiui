#nullable enable

using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerDocumentHost
{
    string ComponentId { get; set; }
    string? EditorText { get; }
    bool IsEditorReadOnly { get; }
    void SetEditorText(string text);
    void SetBaseDirectory(string value);
    void RebuildPresetStructure();
    void UpdateSimpleDesignerAvailability();
    void NotifyDocumentStateChanged();
    void AttachExistingPresetFiles();
    Task ApplyAutoCorrectorSilentlyAsync(ConfigItem item);
    string GetConfigItemBaseDirectory(ConfigItem item);
    Task<ConfigMakerSavePresetRequest?> AskSavePresetAsync(string suggestedName, bool overwriteEnabled);
    Task<bool> ConfirmNewDocumentAsync();
    string? ChooseExportPath(bool allowTextFormat);
    void ClearPresetStructure();
    void ClearEditorUndoHistory();
    string Text(string key);
    void ShowDocumentMessage(string message, bool isError);
}
