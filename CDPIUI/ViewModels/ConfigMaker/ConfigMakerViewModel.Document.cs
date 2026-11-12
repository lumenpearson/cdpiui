using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// Loading, saving and exporting the edited preset document.
public sealed partial class ConfigMakerViewModel
{
    public event EventHandler DocumentStateChanged;

    public ObservableCollection<ConfigMakerVariableDefinition> PresetVariables => _presetDocument.Variables;
    public ObservableCollection<ConfigMakerPresetResource> PresetResources => _presetDocument.Resources;
    public bool HasVariables => _presetDocument.HasVariables;
    public bool CanExportText => !HasVariables;

    public void LoadPresetDocument(ConfigMakerPresetDocument document) =>
        _documentCoordinator.LoadPresetDocument(document);

    public Task LoadConfigItem(ConfigItem item, bool applyAutoCorrectorSilently = false) =>
        _documentCoordinator.LoadConfigItem(item, applyAutoCorrectorSilently);

    public ConfigItem CreateConfigItem(string packId, string presetName) =>
        _documentCoordinator.CreateConfigItem(packId, presetName);

    public Task<ConfigMakerPresetSaveResult> SaveToApplicationAsync() =>
        _documentCoordinator.SaveToApplicationAsync();

    public Task NewDocumentAsync() => _documentCoordinator.NewDocumentAsync();

    public Task SaveTextAsync() => _documentCoordinator.SaveTextAsync();

    string IConfigMakerDocumentHost.ComponentId { get => ComponentId; set => ComponentId = value; }
    bool IConfigMakerDocumentHost.IsEditorReadOnly => IsEditorReadOnly;
    string IConfigMakerDocumentHost.EditorText => _view.EditorText;
    void IConfigMakerDocumentHost.SetEditorText(string text) => SetEditorText(text);
    void IConfigMakerDocumentHost.SetBaseDirectory(string value) => _presetResources.BaseDirectory = value;
    void IConfigMakerDocumentHost.RebuildPresetStructure() => RebuildPresetStructure();
    void IConfigMakerDocumentHost.UpdateSimpleDesignerAvailability() => UpdateSimpleDesignerAvailability();
    void IConfigMakerDocumentHost.NotifyDocumentStateChanged() => DocumentStateChanged?.Invoke(this, EventArgs.Empty);
    void IConfigMakerDocumentHost.AttachExistingPresetFiles() => AttachExistingPresetFiles();
    Task IConfigMakerDocumentHost.ApplyAutoCorrectorSilentlyAsync(ConfigItem item) => ApplyAutoCorrectorSilentlyAsync(item);
    string IConfigMakerDocumentHost.GetConfigItemBaseDirectory(ConfigItem item) => ConfigMakerPresetResources.GetConfigItemBaseDirectory(item);
    Task<ConfigMakerSavePresetRequest> IConfigMakerDocumentHost.AskSavePresetAsync(string suggestedName, bool overwriteEnabled) =>
        _view.AskSavePresetAsync(suggestedName, overwriteEnabled);
    Task<bool> IConfigMakerDocumentHost.ConfirmNewDocumentAsync() => _view.ConfirmAsync(
        _localizer.GetLocalizedString("ConfigMakerNewConfirmationTitle"),
        _localizer.GetLocalizedString("ConfigMakerNewConfirmationMessage"),
        _localizer.GetLocalizedString("Continue"));
    string IConfigMakerDocumentHost.ChooseExportPath(bool allowTextFormat) => _view.ChooseExportPath(allowTextFormat);
    void IConfigMakerDocumentHost.ClearPresetStructure() => ClearPresetStructure();
    void IConfigMakerDocumentHost.ClearEditorUndoHistory() => _view.ClearEditorUndoHistory();
    string IConfigMakerDocumentHost.Text(string key) => _localizer.GetLocalizedString(key);
    void IConfigMakerDocumentHost.ShowDocumentMessage(string message, bool isError) =>
        ShowEditorMessage(message, isError ? InfoBarSeverity.Error : InfoBarSeverity.Success);

    private async Task ApplyAutoCorrectorSilentlyAsync(ConfigItem sourceConfig)
    {
        ConfigImportAutoCorrector autoCorrector = new();
        foreach (ConfigMakerPresetFileInfo file in _presetResources.ExtractFiles(CommandText).ToArray())
        {
            string missingPath = _presetResources.TryResolvePath(file.Path);
            if (string.IsNullOrWhiteSpace(missingPath) || File.Exists(missingPath))
            {
                continue;
            }
            try
            {
                if (autoCorrector.ShouldSuggestEmptyFile(missingPath))
                {
                    TryCreateEmptyPresetFile(missingPath, showStatus: false);
                    continue;
                }
                ConfigImportResult autoCorrectResult = _presetResources.CreateAutoCorrectResult(
                    missingPath,
                    sourceConfig);
                string suggestion = await Task.Run(() => autoCorrector.FindReplacement(
                    autoCorrectResult,
                    missingPath));
                if (!string.IsNullOrWhiteSpace(suggestion) && File.Exists(suggestion))
                {
                    TryApplyPresetFileReplacement(file, suggestion, showStatus: false);
                }
            }
            catch (Exception ex)
            {
                // A silent import leaves unresolved references unchanged.
                Core.Basic.Logger.Instance.CreateWarningLog(LogSource, $"Cannot resolve \"{missingPath}\": {ex.Message}");
            }
        }
    }

    private void AttachExistingPresetFiles()
    {
        string updatedCommand = _presetResources.AttachExistingFiles();
        if (!string.Equals(updatedCommand, CommandText, StringComparison.Ordinal))
        {
            SetEditorText(ComponentCommandLineFormatter.FormatByFlags(updatedCommand));
        }
    }

    private void SyncDocumentFromEditor() => _documentCoordinator.SyncDocumentFromEditor();

    private void PresetDocument_ContentChanged(object sender, EventArgs e)
    {
        DocumentStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
