using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Controls.CreateConfigHelper;

// Loading, saving and exporting the edited preset document.
public sealed partial class ConfigMakerUserControl
{
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
        ConfigMakerSavePresetContentDialog dialog = new(_presetDocument.Name)
        {
            XamlRoot = XamlRoot,
            OverwriteEnable = !string.IsNullOrWhiteSpace(_presetDocument.PackId) &&
                              !string.IsNullOrWhiteSpace(_presetDocument.FileName),
        };
        if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
        {
            return ConfigMakerPresetSaveResult.Failed("CANCELED");
        }

        ConfigMakerPresetStorageService storageService = new();
        bool overwrite = dialog.Result == ConfigMakerSavePresetContentDialogResult.Overwrite;
        ConfigMakerPresetSaveResult result = overwrite
            ? await storageService.OverwriteAsync(_presetDocument)
            : await storageService.SaveAsync(dialog.PresetName, _presetDocument);
        if (result.Success)
        {
            if (!overwrite)
            {
                _presetDocument.Name = dialog.PresetName;
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
        if (!string.IsNullOrWhiteSpace(CommandEditor.Text) ||
            _presetDocument.HasVariables ||
            _presetDocument.HasResources)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = _localizer.GetLocalizedString("ConfigMakerNewConfirmationTitle"),
                Content = _localizer.GetLocalizedString("ConfigMakerNewConfirmationMessage"),
                PrimaryButtonText = _localizer.GetLocalizedString("Continue"),
                CloseButtonText = _localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        SetEditorText(string.Empty);
        _presetResources.BaseDirectory = string.Empty;
        _presetDocument.Name = string.Empty;
        _presetDocument.Variables.Clear();
        _presetDocument.Resources.Clear();
        ClearPresetStructure();
        CommandEditor.ClearUndoRedoHistory();
    }

    public async Task SaveTextAsync()
    {
        try
        {
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                OverwritePrompt = true,
                FileName = "config" + ConfigMakerPresetExporter.SharePackageExtension,
                DefaultExt = ConfigMakerPresetExporter.SharePackageExtension,
                Filter = (!HasVariables ? _localizer.GetLocalizedString("ConfigMakerTextFileFilter") + "|" : "") + _localizer.GetLocalizedString("ConfigMakerCCFileFilter"),
            };
            dialog.FilterIndex = dialog.Filter.Split('|').Length / 2;
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            SyncDocumentFromEditor();
            await ConfigMakerPresetExporter.ExportAsync(_presetDocument, dialog.FileName);

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
                Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerUserControl), $"Cannot resolve \"{missingPath}\": {ex.Message}");
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
        _presetDocument.CommandText = CommandEditor.Text ?? string.Empty;
    }

    private void PresetDocument_ContentChanged(object sender, EventArgs e)
    {
        DocumentStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
