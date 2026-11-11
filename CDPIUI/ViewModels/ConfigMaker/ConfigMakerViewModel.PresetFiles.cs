using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The actions of the preset file tree: open, show, add, remove and replace missing files.
public sealed partial class ConfigMakerViewModel
{
    public event EventHandler<ConfigMakerPresetFileReplacedEventArgs> PresetFileReplaced;

    public RelayCommand AddPresetFileCommand { get; }

    public void AddPresetFiles(IEnumerable<string> filePaths) => _presetFileActions.AddPresetFiles(filePaths);

    public Task ReplacePresetFileAsync(ConfigMakerPresetFileInfo file) =>
        _presetFileReplacer.ReplacePresetFileAsync(file);

    public Task OpenPresetFileAsync(ConfigMakerPresetFileInfo file) => _presetFileActions.OpenPresetFileAsync(file);
    public void ShowPresetFileInFolder(ConfigMakerPresetFileInfo file) => _presetFileActions.ShowPresetFileInFolder(file);
    public Task RemovePresetFileAsync(ConfigMakerPresetFileInfo file) => _presetFileActions.RemovePresetFileAsync(file);

    /// <summary>Inserts the reference of a file of the tree at the editor cursor, attaching the file first.</summary>
    public void InsertPresetFileReference(ConfigMakerPresetFileInfo file) => _presetFileActions.InsertPresetFileReference(file);

    private void AddPresetFilesFromPicker() => _presetFileActions.AddPresetFilesFromPicker();

    private bool TryCreateEmptyPresetFile(string missingPath, bool showStatus = true) =>
        _presetFileReplacer.TryCreateEmptyPresetFile(missingPath, showStatus);

    private bool TryApplyPresetFileReplacement(ConfigMakerPresetFileInfo file, string replacementPath, bool showStatus) =>
        _presetFileReplacer.TryApplyPresetFileReplacement(file, replacementPath, showStatus);

    Task IConfigMakerPresetFileReplacementHost.RunReportingErrorsAsync(Func<Task> action) => RunReportingErrorsAsync(action);
    Task<ConfigMakerFileReplacementChoice> IConfigMakerPresetFileReplacementHost.AskPresetFileReplacementAsync(string path) => _view.AskPresetFileReplacementAsync(path);
    string IConfigMakerPresetFileReplacementHost.ChooseReplacementFile(string path) => _view.ChooseReplacementFile(path);
    string IConfigMakerPresetFileReplacementHost.Text(string key) => _localizer.GetLocalizedString(key);
    void IConfigMakerPresetFileReplacementHost.RebuildPresetStructure() => RebuildPresetStructure();
    bool IConfigMakerPresetFileReplacementHost.UsesExplicitPresetStructure => _presetStructure.UsesExplicitPresetStructure;
    ConfigMakerPresetFileInfo[] IConfigMakerPresetFileReplacementHost.ExplicitPresetFiles
    {
        get => _presetStructure.ExplicitPresetFiles;
        set => _presetStructure.ExplicitPresetFiles = value;
    }
    void IConfigMakerPresetFileReplacementHost.SetCommandText(string text) => CommandText = text;
    void IConfigMakerPresetFileReplacementHost.NotifyPresetFileReplaced(string originalPath, string replacementPath) =>
        PresetFileReplaced?.Invoke(this, new ConfigMakerPresetFileReplacedEventArgs(CommandText, originalPath, replacementPath));
    void IConfigMakerPresetFileReplacementHost.ShowPresetFileMessage(string message, ConfigMakerPresetFileNoticeSeverity severity) =>
        ShowEditorMessage(message, severity switch
        {
            ConfigMakerPresetFileNoticeSeverity.Success => InfoBarSeverity.Success,
            ConfigMakerPresetFileNoticeSeverity.Warning => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Error,
        });

    bool IConfigMakerPresetFileActionsHost.IsEditorReadOnly => IsEditorReadOnly;
    Task IConfigMakerPresetFileActionsHost.RunReportingErrorsAsync(Func<Task> action) => RunReportingErrorsAsync(action);
    IReadOnlyList<string> IConfigMakerPresetFileActionsHost.ChoosePresetFilesToAdd() => _view.ChoosePresetFilesToAdd();
    void IConfigMakerPresetFileActionsHost.AttachExistingPresetFiles() => AttachExistingPresetFiles();
    void IConfigMakerPresetFileActionsHost.RebuildPresetStructure() => RebuildPresetStructure();
    void IConfigMakerPresetFileActionsHost.SetPresetFilesPanelVisible(bool visible) => _view.SetPresetFilesPanelVisible(visible);
    string IConfigMakerPresetFileActionsHost.Text(string key) => _localizer.GetLocalizedString(key);
    Task<bool> IConfigMakerPresetFileActionsHost.AskTextFileApplicationAsync(string path) => _view.AskTextFileApplicationAsync(path);
    Task<bool> IConfigMakerPresetFileActionsHost.ConfirmRemoveReferencedFileAsync() => _view.ConfirmAsync(
        _localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogTitle"),
        _localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogMessage"),
        _localizer.GetLocalizedString("Remove"));
    void IConfigMakerPresetFileActionsHost.ScheduleDiagnostics() => ScheduleDiagnostics();
    void IConfigMakerPresetFileActionsHost.InsertAtCursor(string reference) => InsertAtCursor(reference);
    void IConfigMakerPresetFileActionsHost.ShowPresetFileMessage(string message, ConfigMakerPresetFileNoticeSeverity severity) =>
        ShowEditorMessage(message, severity switch
        {
            ConfigMakerPresetFileNoticeSeverity.Success => InfoBarSeverity.Success,
            ConfigMakerPresetFileNoticeSeverity.Warning => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Error,
        });
}
