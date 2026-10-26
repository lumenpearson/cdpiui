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

    public void LoadPresetDocument(ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _presetResources.BaseDirectory = string.Empty;
        _presetDocument.Name = document.Name;
        _presetDocument.ComponentId = document.ComponentId;
        _presetDocument.PackId = document.PackId;
        _presetDocument.FileName = document.FileName;
        _presetDocument.Meta = document.Meta;
        _presetDocument.TargetVersion = document.TargetVersion;
        _presetDocument.Variables.Clear();
        foreach (ConfigMakerVariableDefinition variable in document.Variables)
        {
            _presetDocument.Variables.Add(variable.Clone());
        }
        _presetDocument.Resources.Clear();
        foreach (ConfigMakerPresetResource resource in document.Resources)
        {
            _presetDocument.Resources.Add(resource.Clone());
        }
        ComponentId = document.ComponentId;
        SetEditorText(document.CommandText);
        RebuildPresetStructure();
        UpdateSimpleDesignerAvailability();
        DocumentStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadConfigItem(
        ConfigItem item,
        bool applyAutoCorrectorSilently = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        LoadPresetDocument(ConfigMakerPresetDocument.FromConfigItem(item));
        _presetResources.BaseDirectory = ConfigMakerPresetResources.GetConfigItemBaseDirectory(item);
        RebuildPresetStructure();
        if (applyAutoCorrectorSilently)
        {
            await ApplyAutoCorrectorSilentlyAsync(item);
        }
        AttachExistingPresetFiles();
        RebuildPresetStructure();
    }

    public ConfigItem CreateConfigItem(string packId, string presetName)
    {
        SyncDocumentFromEditor();
        return _presetDocument.ToConfigItem(packId, presetName);
    }

    public async Task<ConfigMakerPresetSaveResult> SaveToApplicationAsync()
    {
        SyncDocumentFromEditor();
        AttachExistingPresetFiles();
        SyncDocumentFromEditor();
        ConfigMakerSavePresetRequest request = await _view.AskSavePresetAsync(
            _presetDocument.Name,
            !string.IsNullOrWhiteSpace(_presetDocument.PackId) &&
            !string.IsNullOrWhiteSpace(_presetDocument.FileName));
        if (request == null)
        {
            return ConfigMakerPresetSaveResult.Failed("CANCELED");
        }

        ConfigMakerPresetStorageService storageService = new();
        ConfigMakerPresetSaveResult result = request.Overwrite
            ? await storageService.OverwriteAsync(_presetDocument)
            : await storageService.SaveAsync(request.PresetName, _presetDocument);
        if (result.Success)
        {
            if (!request.Overwrite)
            {
                _presetDocument.Name = request.PresetName;
            }
            _presetDocument.PackId = result.PackId;
            _presetDocument.FileName = result.ConfigFileName;
            ApplyStoredResources(result);
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerPresetSavedMessage"),
                    _presetDocument.Name),
                InfoBarSeverity.Success);
        }
        else if (!string.Equals(result.ErrorCode, "CANCELED", StringComparison.Ordinal))
        {
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerPresetSaveFailedMessage"),
                    string.IsNullOrWhiteSpace(result.ErrorDetails)
                        ? result.ErrorCode
                        : result.ErrorDetails),
                InfoBarSeverity.Error);
        }
        return result;
    }

    public async Task NewDocumentAsync()
    {
        if (IsEditorReadOnly)
        {
            return;
        }
        if ((!string.IsNullOrWhiteSpace(_view.EditorText) ||
             _presetDocument.HasVariables ||
             _presetDocument.HasResources) &&
            !await _view.ConfirmAsync(
                _localizer.GetLocalizedString("ConfigMakerNewConfirmationTitle"),
                _localizer.GetLocalizedString("ConfigMakerNewConfirmationMessage"),
                _localizer.GetLocalizedString("Continue")))
        {
            return;
        }

        SetEditorText(string.Empty);
        _presetResources.BaseDirectory = string.Empty;
        _presetDocument.Name = string.Empty;
        _presetDocument.Variables.Clear();
        _presetDocument.Resources.Clear();
        ClearPresetStructure();
        _view.ClearEditorUndoHistory();
    }

    public async Task SaveTextAsync()
    {
        try
        {
            string path = _view.ChooseExportPath(allowTextFormat: !HasVariables);
            if (path == null)
            {
                return;
            }

            SyncDocumentFromEditor();
            await ConfigMakerPresetExporter.ExportAsync(_presetDocument, path);

            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerTextSavedMessage"),
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerTextSaveFailedMessage"),
                    exception.Message),
                InfoBarSeverity.Error);
        }
    }

    private void ApplyStoredResources(ConfigMakerPresetSaveResult result)
    {
        foreach (ConfigMakerPresetResource resource in _presetDocument.Resources)
        {
            ConfigMakerResourceMetadata? storedResource = result.StoredResources.FirstOrDefault(candidate =>
                string.Equals(candidate.alias, resource.Alias, StringComparison.OrdinalIgnoreCase));
            if (storedResource == null || string.IsNullOrWhiteSpace(storedResource.path))
            {
                continue;
            }
            resource.Path = storedResource.path;
            resource.IsBuiltIn = storedResource.isBuiltIn;
        }
    }

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

    private void SyncDocumentFromEditor()
    {
        _presetDocument.ComponentId = ComponentId;
        _presetDocument.CommandText = _view.EditorText ?? string.Empty;
    }

    private void PresetDocument_ContentChanged(object sender, EventArgs e)
    {
        DocumentStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
