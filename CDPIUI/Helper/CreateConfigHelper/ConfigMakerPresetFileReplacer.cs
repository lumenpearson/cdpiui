#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Helper.CreateConfigHelper;

// Missing-file replacement workflow, shared by the editor dialog and silent imports.
internal sealed class ConfigMakerPresetFileReplacer
{
    private readonly IConfigMakerPresetFileReplacementHost _host;
    private readonly IConfigMakerPresetFileReplacementBackend _backend;

    public ConfigMakerPresetFileReplacer(IConfigMakerPresetFileReplacementHost host,
        IConfigMakerPresetFileReplacementBackend backend)
    {
        _host = host;
        _backend = backend;
    }

    public Task ReplacePresetFileAsync(ConfigMakerPresetFileInfo? file) =>
        _host.RunReportingErrorsAsync(async () =>
        {
            if (file == null)
            {
                return;
            }

            string? missingPath = _backend.TryResolvePath(file.Path);
            if (string.IsNullOrWhiteSpace(missingPath))
            {
                _host.ShowPresetFileMessage(
                    _host.Text("ConfigMakerPresetFileInvalidPathMessage"),
                    ConfigMakerPresetFileNoticeSeverity.Error);
                return;
            }
            if (_backend.FileExists(missingPath))
            {
                _host.RebuildPresetStructure();
                return;
            }

            ConfigMakerFileReplacementChoice choice = await _host.AskPresetFileReplacementAsync(missingPath);
            if (choice == ConfigMakerFileReplacementChoice.AutoCorrect)
            {
                await AutoCorrectPresetFileAsync(file, missingPath);
            }
            else if (choice == ConfigMakerFileReplacementChoice.ChooseManually)
            {
                ChooseAndApplyPresetFileReplacement(file, missingPath);
            }
        });

    private async Task AutoCorrectPresetFileAsync(ConfigMakerPresetFileInfo file, string missingPath)
    {
        IConfigMakerPresetFileAutoCorrector autoCorrector = _backend.CreateAutoCorrector();
        bool suggestEmptyFile = autoCorrector.ShouldSuggestEmptyFile(missingPath);
        if (suggestEmptyFile)
        {
            TryCreateEmptyPresetFile(missingPath);
            return;
        }

        string? suggestion = await autoCorrector.FindReplacementAsync(missingPath);
        if (!string.IsNullOrWhiteSpace(suggestion))
        {
            TryApplyPresetFileReplacement(file, suggestion, showStatus: true);
            return;
        }

        _host.ShowPresetFileMessage(
            _host.Text("ConfigMakerPresetFileNoSuggestionMessage"),
            ConfigMakerPresetFileNoticeSeverity.Warning);
        ChooseAndApplyPresetFileReplacement(file, missingPath);
    }

    private void ChooseAndApplyPresetFileReplacement(ConfigMakerPresetFileInfo file, string missingPath)
    {
        string? selectedPath = _host.ChooseReplacementFile(missingPath);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            TryApplyPresetFileReplacement(file, selectedPath, showStatus: true);
        }
    }

    public bool TryCreateEmptyPresetFile(string missingPath, bool showStatus = true)
    {
        try
        {
            _backend.CreateEmptyFile(missingPath);
            _host.RebuildPresetStructure();
            if (showStatus)
            {
                _host.ShowPresetFileMessage(
                    string.Format(
                        _host.Text("ConfigMakerPresetFileEmptyCreatedMessage"),
                        missingPath),
                    ConfigMakerPresetFileNoticeSeverity.Success);
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

    public bool TryApplyPresetFileReplacement(
        ConfigMakerPresetFileInfo file,
        string replacementPath,
        bool showStatus)
    {
        try
        {
            string fullReplacementPath = Path.GetFullPath(replacementPath);
            if (!_backend.FileExists(fullReplacementPath))
            {
                throw new FileNotFoundException(null, fullReplacementPath);
            }

            if (_backend.TryUpdateAttachedResource(file, fullReplacementPath))
            {
                _host.RebuildPresetStructure();
                if (showStatus)
                {
                    ShowPresetFileReplaced(fullReplacementPath);
                }
                return true;
            }

            ConfigMakerPresetFileReplacement replacement = _backend.ReplaceReferences(
                file,
                fullReplacementPath) ?? throw new InvalidOperationException(
                    _host.Text("ConfigMakerPresetFileReferenceNotFoundMessage"));

            if (_host.UsesExplicitPresetStructure)
            {
                _host.ExplicitPresetFiles = _host.ExplicitPresetFiles
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
                _host.SetCommandText(ComponentCommandLineFormatter.FormatByFlags(replacement.UpdatedCommandText));
            }
            _host.RebuildPresetStructure();
            _host.NotifyPresetFileReplaced(file.Path, replacement.Reference);
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
        _host.ShowPresetFileMessage(
            string.Format(
                _host.Text("ConfigMakerPresetFileReplacedMessage"),
                fullReplacementPath),
            ConfigMakerPresetFileNoticeSeverity.Success);

    private void ShowPresetFileReplaceFailed(Exception exception) =>
        _host.ShowPresetFileMessage(
            string.Format(
                _host.Text("ConfigMakerPresetFileReplaceFailedMessage"),
                exception.Message),
            ConfigMakerPresetFileNoticeSeverity.Error);
}
