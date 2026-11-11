#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// File-tree actions keep their original ordering around dialogs and resource notifications.
internal sealed class ConfigMakerPresetFileActions
{
    private readonly IList<ConfigMakerPresetResource> _resources;
    private readonly IConfigMakerPresetFileActionsHost _host;
    private readonly IConfigMakerPresetFileActionsBackend _backend;

    public ConfigMakerPresetFileActions(IList<ConfigMakerPresetResource> resources,
        IConfigMakerPresetFileActionsHost host, IConfigMakerPresetFileActionsBackend backend)
    {
        _resources = resources;
        _host = host;
        _backend = backend;
    }

    public void AddPresetFiles(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        int added = _backend.AddFiles(filePaths);
        if (added > 0)
        {
            _host.AttachExistingPresetFiles();
            _host.RebuildPresetStructure();
            _host.SetPresetFilesPanelVisible(true);
            _host.ShowPresetFileMessage(
                string.Format(
                    _host.Text("ConfigMakerPresetFilesAddedMessage"),
                    added),
                ConfigMakerPresetFileNoticeSeverity.Success);
        }
    }

    public Task OpenPresetFileAsync(ConfigMakerPresetFileInfo? file) =>
        _host.RunReportingErrorsAsync(async () =>
        {
            if (file == null)
            {
                return;
            }

            string path = _backend.ResolvePath(file.Path);
            if (!_backend.FileExists(path))
            {
                ShowPresetFileMissing(path);
                return;
            }

            if (file.Kind == ConfigMakerPresetFileKind.SiteList ||
                string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                if (_backend.ShouldAskForTextApplication())
                {
                    if (await _host.AskTextFileApplicationAsync(path))
                    {
                        _backend.RememberTextFileDialogShown();
                    }
                    return;
                }

                _backend.OpenTextFile(path);
                return;
            }

            _backend.OpenFileInDefaultApp(path);
        });

    public void ShowPresetFileInFolder(ConfigMakerPresetFileInfo? file)
    {
        if (file == null)
        {
            return;
        }

        string path = _backend.ResolvePath(file.Path);
        if (!_backend.FileExists(path))
        {
            ShowPresetFileMissing(path);
            return;
        }
        _backend.ShowFileInFolder(path);
    }

    public Task RemovePresetFileAsync(ConfigMakerPresetFileInfo? file) =>
        _host.RunReportingErrorsAsync(async () =>
        {
            ConfigMakerPresetResource? resource = file == null ? null : _backend.FindAttached(file.Path);
            if (resource == null)
            {
                return;
            }
            if (_backend.IsReferenced(resource) &&
                !await _host.ConfirmRemoveReferencedFileAsync())
            {
                return;
            }
            _resources.Remove(resource);
            _host.RebuildPresetStructure();
            _host.ScheduleDiagnostics();
        });

    /// <summary>Inserts the reference of a file of the tree at the editor cursor, attaching the file first.</summary>
    public void InsertPresetFileReference(ConfigMakerPresetFileInfo? file)
    {
        if (_host.IsEditorReadOnly || file == null)
        {
            return;
        }
        string reference = file.Path.Trim().Trim('"');
        if (!reference.StartsWith("preset://", StringComparison.OrdinalIgnoreCase))
        {
            string? resolvedPath = _backend.TryResolvePath(reference);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && _backend.FileExists(resolvedPath))
            {
                ConfigMakerPresetResource resource = _backend.GetOrCreate(
                    Path.GetFullPath(resolvedPath),
                    file.Kind,
                    out bool resourceAdded);
                if (resourceAdded)
                {
                    _host.RebuildPresetStructure();
                }
                reference = resource.Reference;
            }
        }
        _host.InsertAtCursor($"\"{reference}\"");
    }

    public void AddPresetFilesFromPicker()
    {
        IReadOnlyList<string> filePaths = _host.ChoosePresetFilesToAdd();
        if (filePaths.Count > 0)
        {
            AddPresetFiles(filePaths);
        }
    }

    private void ShowPresetFileMissing(string path) =>
        _host.ShowPresetFileMessage(
            string.Format(_host.Text("ConfigMakerPresetFileMissingMessage"), path),
            ConfigMakerPresetFileNoticeSeverity.Warning);
}
