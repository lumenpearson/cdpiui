using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.System;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The actions of the preset file tree: open, show, add, remove and replace missing files.
public sealed partial class ConfigMakerViewModel
{
    public event EventHandler<ConfigMakerPresetFileReplacedEventArgs> PresetFileReplaced;

    public RelayCommand AddPresetFileCommand { get; }

    public void AddPresetFiles(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        int added = _presetResources.AddFiles(filePaths);
        if (added > 0)
        {
            AttachExistingPresetFiles();
            RebuildPresetStructure();
            _view.SetPresetFilesPanelVisible(true);
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerPresetFilesAddedMessage"),
                    added),
                InfoBarSeverity.Success);
        }
    }

    public Task ReplacePresetFileAsync(ConfigMakerPresetFileInfo file) =>
        RunReportingErrorsAsync(async () =>
        {
            if (file == null)
            {
                return;
            }

            string missingPath = _presetResources.TryResolvePath(file.Path);
            if (string.IsNullOrWhiteSpace(missingPath))
            {
                ShowEditorMessage(
                    _localizer.GetLocalizedString("ConfigMakerPresetFileInvalidPathMessage"),
                    InfoBarSeverity.Error);
                return;
            }
            if (File.Exists(missingPath))
            {
                RebuildPresetStructure();
                return;
            }

            ConfigMakerFileReplacementChoice choice = await _view.AskPresetFileReplacementAsync(missingPath);
            if (choice == ConfigMakerFileReplacementChoice.AutoCorrect)
            {
                await AutoCorrectPresetFileAsync(file, missingPath);
            }
            else if (choice == ConfigMakerFileReplacementChoice.ChooseManually)
            {
                ChooseAndApplyPresetFileReplacement(file, missingPath);
            }
        });

    public Task OpenPresetFileAsync(ConfigMakerPresetFileInfo file) =>
        RunReportingErrorsAsync(async () =>
        {
            if (file == null)
            {
                return;
            }

            string path = _presetResources.ResolvePath(file.Path);
            if (!File.Exists(path))
            {
                ShowPresetFileMissing(path);
                return;
            }

            if (file.Kind == ConfigMakerPresetFileKind.SiteList ||
                string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                if (TextFileOpenPreferences.ShouldAskForApplication())
                {
                    if (await _view.AskTextFileApplicationAsync(path))
                    {
                        TextFileOpenPreferences.RememberDialogShown();
                    }
                    return;
                }

                ShellHelper.OpenFile(path);
                return;
            }

            ShellHelper.OpenFileInDefaultApp(path);
        });

    public void ShowPresetFileInFolder(ConfigMakerPresetFileInfo file)
    {
        if (file == null)
        {
            return;
        }

        string path = _presetResources.ResolvePath(file.Path);
        if (!File.Exists(path))
        {
            ShowPresetFileMissing(path);
            return;
        }
        ShellHelper.LookupFileInDirectory(path);
    }

    public Task RemovePresetFileAsync(ConfigMakerPresetFileInfo file) =>
        RunReportingErrorsAsync(async () =>
        {
            ConfigMakerPresetResource resource = file == null ? null : _presetResources.FindAttached(file.Path);
            if (resource == null)
            {
                return;
            }
            if (_presetResources.IsReferenced(resource) &&
                !await _view.ConfirmAsync(
                    _localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogTitle"),
                    _localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogMessage"),
                    _localizer.GetLocalizedString("Remove")))
            {
                return;
            }
            _presetDocument.Resources.Remove(resource);
            RebuildPresetStructure();
            ScheduleDiagnostics();
        });

    /// <summary>Inserts the reference of a file of the tree at the editor cursor, attaching the file first.</summary>
    public void InsertPresetFileReference(ConfigMakerPresetFileInfo file)
    {
        if (IsEditorReadOnly || file == null)
        {
            return;
        }
        string reference = file.Path.Trim().Trim('"');
        if (!reference.StartsWith("preset://", StringComparison.OrdinalIgnoreCase))
        {
            string resolvedPath = _presetResources.TryResolvePath(reference);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
            {
                ConfigMakerPresetResource resource = _presetResources.GetOrCreate(
                    Path.GetFullPath(resolvedPath),
                    file.Kind,
                    out bool resourceAdded);
                if (resourceAdded)
                {
                    RebuildPresetStructure();
                }
                reference = resource.Reference;
            }
        }
        InsertAtCursor($"\"{reference}\"");
    }

    private void AddPresetFilesFromPicker()
    {
        IReadOnlyList<string> filePaths = _view.ChoosePresetFilesToAdd();
        if (filePaths.Count > 0)
        {
            AddPresetFiles(filePaths);
        }
    }

    private async Task AutoCorrectPresetFileAsync(ConfigMakerPresetFileInfo file, string missingPath)
    {
        ConfigImportAutoCorrector autoCorrector = new();
        bool suggestEmptyFile = autoCorrector.ShouldSuggestEmptyFile(missingPath);
        if (suggestEmptyFile)
        {
            TryCreateEmptyPresetFile(missingPath);
            return;
        }

        ConfigImportResult autoCorrectResult = _presetResources.CreateAutoCorrectResult(missingPath);
        string suggestion = await Task.Run(() => autoCorrector.FindReplacement(
            autoCorrectResult,
            missingPath));
        if (!string.IsNullOrWhiteSpace(suggestion))
        {
            TryApplyPresetFileReplacement(file, suggestion, showStatus: true);
            return;
        }

        ShowEditorMessage(
            _localizer.GetLocalizedString("ConfigMakerPresetFileNoSuggestionMessage"),
            InfoBarSeverity.Warning);
        ChooseAndApplyPresetFileReplacement(file, missingPath);
    }

    private void ChooseAndApplyPresetFileReplacement(ConfigMakerPresetFileInfo file, string missingPath)
    {
        string selectedPath = _view.ChooseReplacementFile(missingPath);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            TryApplyPresetFileReplacement(file, selectedPath, showStatus: true);
        }
    }

    private bool TryCreateEmptyPresetFile(string missingPath, bool showStatus = true)
    {
        try
        {
            ConfigMakerPresetResources.CreateEmptyFile(missingPath);
            RebuildPresetStructure();
            if (showStatus)
            {
                ShowEditorMessage(
                    string.Format(
                        _localizer.GetLocalizedString("ConfigMakerPresetFileEmptyCreatedMessage"),
                        missingPath),
                    InfoBarSeverity.Success);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (showStatus)
            {
                ShowPresetFileReplaceFailed(ex);
            }
            return false;
        }
    }

    private bool TryApplyPresetFileReplacement(
        ConfigMakerPresetFileInfo file,
        string replacementPath,
        bool showStatus)
    {
        try
        {
            string fullReplacementPath = Path.GetFullPath(replacementPath);
            if (!File.Exists(fullReplacementPath))
            {
                throw new FileNotFoundException(null, fullReplacementPath);
            }

            if (_presetResources.TryUpdateAttachedResource(file, fullReplacementPath))
            {
                RebuildPresetStructure();
                if (showStatus)
                {
                    ShowPresetFileReplaced(fullReplacementPath);
                }
                return true;
            }

            ConfigMakerPresetFileReplacement replacement = _presetResources.ReplaceReferences(
                file,
                fullReplacementPath) ?? throw new InvalidOperationException(
                    _localizer.GetLocalizedString("ConfigMakerPresetFileReferenceNotFoundMessage"));

            if (_usesExplicitPresetStructure)
            {
                _explicitPresetFiles = _explicitPresetFiles
                    .Select(item => ReferenceEquals(item, file) ||
                        (item.Kind == file.Kind && ConfigFileReferences.PathsEqual(item.Path, file.Path))
                            ? item with
                            {
                                Name = Path.GetFileName(fullReplacementPath),
                                Path = replacement.Reference,
                                Folder = ConfigFileReferences.GetDisplayFolder(replacement.Reference),
                            }
                            : item)
                    .ToArray();
            }
            if (replacement.CommandChanged)
            {
                CommandText = ComponentCommandLineFormatter.FormatByFlags(replacement.UpdatedCommandText);
            }
            RebuildPresetStructure();
            PresetFileReplaced?.Invoke(
                this,
                new ConfigMakerPresetFileReplacedEventArgs(
                    CommandText,
                    file.Path,
                    replacement.Reference));
            if (showStatus)
            {
                ShowPresetFileReplaced(fullReplacementPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (showStatus)
            {
                ShowPresetFileReplaceFailed(ex);
            }
            return false;
        }
    }

    private void ShowPresetFileReplaced(string fullReplacementPath) =>
        ShowEditorMessage(
            string.Format(
                _localizer.GetLocalizedString("ConfigMakerPresetFileReplacedMessage"),
                fullReplacementPath),
            InfoBarSeverity.Success);

    private void ShowPresetFileReplaceFailed(Exception exception) =>
        ShowEditorMessage(
            string.Format(
                _localizer.GetLocalizedString("ConfigMakerPresetFileReplaceFailedMessage"),
                exception.Message),
            InfoBarSeverity.Error);

    private void ShowPresetFileMissing(string path) =>
        ShowEditorMessage(
            string.Format(_localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"), path),
            InfoBarSeverity.Warning);
}
