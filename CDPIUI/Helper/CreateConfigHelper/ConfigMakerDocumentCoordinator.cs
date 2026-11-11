#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Helper.CreateConfigHelper;

// Coordinates the document workflow; the host supplies live editor state and UI effects.
internal sealed class ConfigMakerDocumentCoordinator
{
    private readonly ConfigMakerPresetDocument _document;
    private readonly IConfigMakerDocumentHost _host;
    private readonly Func<IConfigMakerDocumentStorage> _createStorage;
    private readonly Func<ConfigMakerPresetDocument, string, Task> _export;

    public ConfigMakerDocumentCoordinator(
        ConfigMakerPresetDocument document,
        IConfigMakerDocumentHost host,
        Func<IConfigMakerDocumentStorage> createStorage,
        Func<ConfigMakerPresetDocument, string, Task> export)
    {
        _document = document;
        _host = host;
        _createStorage = createStorage;
        _export = export;
    }

    public void LoadPresetDocument(ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _host.SetBaseDirectory(string.Empty);
        _document.Name = document.Name;
        _document.ComponentId = document.ComponentId;
        _document.PackId = document.PackId;
        _document.FileName = document.FileName;
        _document.Meta = document.Meta;
        _document.TargetVersion = document.TargetVersion;
        _document.Variables.Clear();
        foreach (ConfigMakerVariableDefinition variable in document.Variables)
        {
            _document.Variables.Add(variable.Clone());
        }
        _document.Resources.Clear();
        foreach (ConfigMakerPresetResource resource in document.Resources)
        {
            _document.Resources.Add(resource.Clone());
        }
        _host.ComponentId = document.ComponentId;
        _host.SetEditorText(document.CommandText);
        _host.RebuildPresetStructure();
        _host.UpdateSimpleDesignerAvailability();
        _host.NotifyDocumentStateChanged();
    }

    public async Task LoadConfigItem(
        ConfigItem item,
        bool applyAutoCorrectorSilently = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        LoadPresetDocument(ConfigMakerPresetDocument.FromConfigItem(item));
        _host.SetBaseDirectory(_host.GetConfigItemBaseDirectory(item));
        _host.RebuildPresetStructure();
        if (applyAutoCorrectorSilently)
        {
            await _host.ApplyAutoCorrectorSilentlyAsync(item);
        }
        _host.AttachExistingPresetFiles();
        _host.RebuildPresetStructure();
    }

    public ConfigItem CreateConfigItem(string packId, string presetName)
    {
        SyncDocumentFromEditor();
        return _document.ToConfigItem(packId, presetName);
    }

    public async Task<ConfigMakerPresetSaveResult> SaveToApplicationAsync()
    {
        SyncDocumentFromEditor();
        _host.AttachExistingPresetFiles();
        SyncDocumentFromEditor();
        ConfigMakerSavePresetRequest? request = await _host.AskSavePresetAsync(
            _document.Name,
            !string.IsNullOrWhiteSpace(_document.PackId) &&
            !string.IsNullOrWhiteSpace(_document.FileName));
        if (request == null)
        {
            return ConfigMakerPresetSaveResult.Failed("CANCELED");
        }

        IConfigMakerDocumentStorage storageService = _createStorage();
        ConfigMakerPresetSaveResult result = request.Overwrite
            ? await storageService.OverwriteAsync(_document)
            : await storageService.SaveAsync(request.PresetName, _document);
        if (result.Success)
        {
            if (!request.Overwrite)
            {
                _document.Name = request.PresetName;
            }
            _document.PackId = result.PackId;
            _document.FileName = result.ConfigFileName;
            ApplyStoredResources(result);
            _host.ShowDocumentMessage(
                string.Format(
                    _host.Text("ConfigMakerPresetSavedMessage"),
                    _document.Name),
                isError: false);
        }
        else if (!string.Equals(result.ErrorCode, "CANCELED", StringComparison.Ordinal))
        {
            _host.ShowDocumentMessage(
                string.Format(
                    _host.Text("ConfigMakerPresetSaveFailedMessage"),
                    string.IsNullOrWhiteSpace(result.ErrorDetails)
                        ? result.ErrorCode
                        : result.ErrorDetails),
                isError: true);
        }
        return result;
    }

    public async Task NewDocumentAsync()
    {
        if (_host.IsEditorReadOnly)
        {
            return;
        }
        if ((!string.IsNullOrWhiteSpace(_host.EditorText) ||
             _document.HasVariables ||
             _document.HasResources) &&
            !await _host.ConfirmNewDocumentAsync())
        {
            return;
        }

        _host.SetEditorText(string.Empty);
        _host.SetBaseDirectory(string.Empty);
        _document.Name = string.Empty;
        _document.Variables.Clear();
        _document.Resources.Clear();
        _host.ClearPresetStructure();
        _host.ClearEditorUndoHistory();
    }

    public async Task SaveTextAsync()
    {
        try
        {
            string? path = _host.ChooseExportPath(allowTextFormat: !_document.HasVariables);
            if (path == null)
            {
                return;
            }

            SyncDocumentFromEditor();
            await _export(_document, path);

            _host.ShowDocumentMessage(
                _host.Text("ConfigMakerTextSavedMessage"),
                isError: false);
        }
        catch (Exception exception)
        {
            _host.ShowDocumentMessage(
                string.Format(
                    _host.Text("ConfigMakerTextSaveFailedMessage"),
                    exception.Message),
                isError: true);
        }
    }

    private void ApplyStoredResources(ConfigMakerPresetSaveResult result)
    {
        foreach (ConfigMakerPresetResource resource in _document.Resources)
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

    public void SyncDocumentFromEditor()
    {
        _document.ComponentId = _host.ComponentId;
        _document.CommandText = _host.EditorText ?? string.Empty;
    }
}
