using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.Extensions;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// The file references of the edited preset: path resolution, attached "preset://" resources
// and the rewriting of file references in the command and in the variables.
internal sealed class ConfigMakerPresetResources
{
    private readonly ConfigMakerPresetResourceRegistry _registry;
    private readonly ConfigMakerPresetReferenceEditor _referenceEditor;
    private readonly ConfigMakerPresetDocument _document;
    private readonly Func<string> _getComponentId;
    private readonly Func<string> _getCommandText;

    public ConfigMakerPresetResources(
        ConfigMakerPresetDocument document,
        Func<string> getComponentId,
        Func<string> getCommandText)
    {
        _document = document;
        _getComponentId = getComponentId;
        _getCommandText = getCommandText;
        _registry = new ConfigMakerPresetResourceRegistry(_document.Resources, TryResolvePath, IsPathInsideComponent);
        _referenceEditor = new ConfigMakerPresetReferenceEditor(
            _document.Variables, _registry, ExtractFilesFromText, TryExtractDirectFile);
    }

    // The directory of the loaded preset pack; empty when the preset is not stored yet.
    public string BaseDirectory { get; set; } = string.Empty;

    private string ComponentId => _getComponentId();

    private string ComponentDirectory => DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory;

    public static string GetConfigItemBaseDirectory(ConfigItem item)
    {
        string packId = item?.packId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(packId))
        {
            return string.Empty;
        }
        string registeredDirectory = DatabaseHelper.Instance.GetItemById(packId)?.Directory ?? string.Empty;
        return !string.IsNullOrWhiteSpace(registeredDirectory)
            ? Path.GetFullPath(registeredDirectory)
            : Path.GetFullPath(Path.Combine(Directories.StoreItemsDirectory, packId));
    }

    public static IEnumerable<string> GetVariableCandidateValues(
        ConfigMakerVariableDefinition variable)
    {
        if (!string.IsNullOrWhiteSpace(variable.Value))
        {
            yield return variable.Value;
        }
        foreach (string value in variable.Values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            yield return value;
        }
        if (!string.IsNullOrWhiteSpace(variable.OnValue))
        {
            yield return variable.OnValue;
        }
        if (!string.IsNullOrWhiteSpace(variable.OffValue))
        {
            yield return variable.OffValue;
        }
    }

    public static string ReplaceFileInText(
        string text,
        ConfigMakerPresetFileInfo file,
        string replacementPath) =>
        ConfigFileReferences.ReplaceFileInText(
            text,
            file.Path,
            ConfigMakerPresetFileKinds.ToCoreFileKind(file.Kind),
            replacementPath);

    public static void CreateEmptyFile(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        using (new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read))
        {
        }
    }

    public string GetBaseDirectory()
    {
        if (!string.IsNullOrWhiteSpace(BaseDirectory))
        {
            return BaseDirectory;
        }
        return ComponentDirectory ?? string.Empty;
    }

    public string ResolvePath(string sourcePath) =>
        CreateSnapshot().ResolveFilePath(
            sourcePath,
            ComponentDirectory ?? Directories.CurrentDirectory,
            GetBaseDirectory());

    public string TryResolvePath(string sourcePath)
    {
        try
        {
            return ResolvePath(sourcePath);
        }
        catch
        {
            return string.Empty;
        }
    }

    public ConfigMakerPresetResource FindAttached(string reference) => _registry.FindAttached(reference);

    public bool IsPathInsideComponent(string filePath)
    {
        string componentDirectory = ComponentDirectory ?? string.Empty;
        return !string.IsNullOrWhiteSpace(componentDirectory) &&
            ConfigMakerPresetPaths.IsPathInside(filePath, componentDirectory);
    }

    public string CreateUniqueAlias(string fileName) => _registry.CreateUniqueAlias(fileName);

    public ConfigItem CreateSnapshot(string commandText = null)
    {
        ConfigItem snapshot = _document.ToConfigItem(string.Empty, _document.Name);
        snapshot.startup_string = commandText ?? _getCommandText();
        return snapshot;
    }

    public IEnumerable<ConfigMakerPresetFileInfo> ExtractFiles(string commandText) =>
        CreateSnapshot(commandText).UsedFiles.Select(ConfigMakerPresetFileKinds.ToEditorFile);

    public IEnumerable<ConfigMakerPresetFileInfo> ExtractFilesFromText(string text)
    {
        ConfigItem snapshot = CreateSnapshot();
        return ConfigFileReferences.ExtractFromText(text, snapshot.ExpandFileReference)
            .Select(ConfigMakerPresetFileKinds.ToEditorFile);
    }

    public ConfigMakerPresetFileInfo TryExtractDirectFile(string value) =>
        ConfigMakerPresetFileKinds.ToEditorFile(
            ConfigFileReferences.ExtractDirectFile(value, CreateSnapshot().ExpandFileReference));

    public IEnumerable<ConfigMakerPresetFileInfo> GetAttachedFiles() => _document.Resources
        .Select(resource => new ConfigMakerPresetFileInfo(
            resource.Alias,
            resource.Reference,
            Path.GetDirectoryName(resource.Path) ?? string.Empty,
            ConfigMakerPresetFileKinds.ToPresetFileKind(resource.Kind),
            IsAttachedResource: true));

    public bool IsReferenced(ConfigMakerPresetResource resource) =>
        _getCommandText().Contains(resource.Reference, StringComparison.OrdinalIgnoreCase) ||
        _document.Variables
            .SelectMany(GetVariableCandidateValues)
            .Any(value => value.Contains(resource.Reference, StringComparison.OrdinalIgnoreCase));

    // Attaches existing files as preset resources. Returns the number of the new resources.
    public int AddFiles(IEnumerable<string> filePaths) => _registry.AddFiles(filePaths);

    public ConfigMakerPresetResource GetOrCreate(ConfigMakerPresetFileInfo file, out bool resourceAdded) =>
        _registry.GetOrCreate(file, out resourceAdded);

    public ConfigMakerPresetResource GetOrCreate(
        string fullPath, ConfigMakerPresetFileKind kind, out bool resourceAdded) =>
        _registry.GetOrCreate(fullPath, kind, out resourceAdded);

    // Replaces the file references of the command and the variables with attached resources.
    // Returns the updated command; the variables are updated in place.
    public string AttachExistingFiles() => _referenceEditor.AttachExistingFiles(_getCommandText());

    // Points an attached resource at the replacement file. Returns false when the file is not an attached resource.
    public bool TryUpdateAttachedResource(ConfigMakerPresetFileInfo file, string fullReplacementPath) =>
        _registry.TryUpdateAttachedResource(file, fullReplacementPath);

    // Attaches the replacement file and rewrites the references to the replaced file.
    // Returns null when neither the command nor a variable refers to the replaced file.
    public ConfigMakerPresetFileReplacement ReplaceReferences(
        ConfigMakerPresetFileInfo file, string fullReplacementPath) =>
        _referenceEditor.ReplaceReferences(_getCommandText, file, fullReplacementPath);

    public ConfigImportResult CreateAutoCorrectResult(
        string missingPath,
        ConfigItem sourceConfig = null)
    {
        var component = DatabaseHelper.Instance.GetItemById(ComponentId);
        string componentDirectory = component?.Directory ?? string.Empty;
        string presetDirectory = GetBaseDirectory();
        string sourceDirectory = Directory.Exists(presetDirectory)
            ? presetDirectory
            : Directory.Exists(componentDirectory)
                ? componentDirectory
            : Path.GetDirectoryName(missingPath) ?? Environment.CurrentDirectory;
        return new ConfigImportResult
        {
            Config = sourceConfig,
            Target = new ConfigImportTarget(
                ComponentId ?? string.Empty,
                component?.ShortName.NullIfEmpty() ?? ComponentId ?? string.Empty,
                component?.Executable ?? string.Empty,
                component?.CurrentVersion,
                componentDirectory),
            SourcePath = Path.Combine(sourceDirectory, ".cdpiui-preset.txt"),
            Issues = [],
            SourceFiles = [],
            ReferencedFiles = [missingPath],
            MissingReferencedFiles = [missingPath],
            MissingFileResolutions = [],
            GeneratedFiles = [],
        };
    }
}
