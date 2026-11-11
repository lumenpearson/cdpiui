#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerPresetFileActionsHost
{
    Task RunReportingErrorsAsync(Func<Task> action);
    IReadOnlyList<string> ChoosePresetFilesToAdd();
    void AttachExistingPresetFiles();
    void RebuildPresetStructure();
    void SetPresetFilesPanelVisible(bool visible);
    string Text(string key);
    void ShowPresetFileMessage(string message, ConfigMakerPresetFileNoticeSeverity severity);
    Task<bool> AskTextFileApplicationAsync(string path);
    Task<bool> ConfirmRemoveReferencedFileAsync();
    void ScheduleDiagnostics();
    bool IsEditorReadOnly { get; }
    void InsertAtCursor(string reference);
}
