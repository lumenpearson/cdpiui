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
using Unidecode.NET;

namespace CDPIUI.Helper.CreateConfigHelper;

// The file references of the edited preset: path resolution, attached "preset://" resources
// and the rewriting of file references in the command and in the variables.
internal sealed class ConfigMakerPresetResources
{
    private const string PresetScheme = "preset://";

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

    public ConfigMakerPresetResource FindAttached(string reference)
    {
        string alias = (reference ?? string.Empty).Trim().Trim('"', '\'');
        if (alias.StartsWith(PresetScheme, StringComparison.OrdinalIgnoreCase))
        {
            alias = alias[PresetScheme.Length..];
        }
        return _document.Resources.FirstOrDefault(resource =>
            string.Equals(resource.Alias, alias, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsPathInsideComponent(string filePath)
    {
        string componentDirectory = ComponentDirectory ?? string.Empty;
        return !string.IsNullOrWhiteSpace(componentDirectory) &&
            ConfigMakerPresetPaths.IsPathInside(filePath, componentDirectory);
    }

    public string CreateUniqueAlias(string fileName)
    {
        string normalized = fileName.Unidecode();
        normalized = string.Concat(normalized.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '_'));
        normalized = normalized.Trim('_');
        if (normalized.Length == 0)
        {
            normalized = "resource.bin";
        }
        string stem = Path.GetFileNameWithoutExtension(normalized);
        string extension = Path.GetExtension(normalized);
        string candidate = normalized;
        int suffix = 2;
        while (_document.Resources.Any(resource =>
                   string.Equals(resource.Alias, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{stem}_{suffix++}{extension}";
        }
        return candidate;
    }

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
    public int AddFiles(IEnumerable<string> filePaths)
    {
        int added = 0;
        foreach (string sourcePath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }
            string fullPath = Path.GetFullPath(sourcePath);
            if (_document.Resources.Any(resource =>
                    string.Equals(
                        TryResolvePath(resource.Reference),
                        fullPath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            string alias = CreateUniqueAlias(Path.GetFileName(fullPath));
            _document.Resources.Add(new ConfigMakerPresetResource
            {
                Alias = alias,
                Path = fullPath,
                Kind = ConfigMakerPresetFileKinds.InferResourceKind(fullPath),
                IsBuiltIn = IsPathInsideComponent(fullPath),
            });
            added++;
        }
        return added;
    }

    public ConfigMakerPresetResource GetOrCreate(
        ConfigMakerPresetFileInfo file,
        out bool resourceAdded)
    {
        resourceAdded = false;
        if (file == null || file.Path.StartsWith(PresetScheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string resolvedPath = TryResolvePath(file.Path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        return GetOrCreate(
            Path.GetFullPath(resolvedPath),
            file.Kind,
            out resourceAdded);
    }

    public ConfigMakerPresetResource GetOrCreate(
        string fullPath,
        ConfigMakerPresetFileKind kind,
        out bool resourceAdded)
    {
        resourceAdded = false;
        ConfigMakerPresetResource resource = _document.Resources.FirstOrDefault(candidate =>
            string.Equals(
                TryResolvePath(candidate.Reference),
                fullPath,
                StringComparison.OrdinalIgnoreCase));
        if (resource != null)
        {
            return resource;
        }

        resource = new ConfigMakerPresetResource
        {
            Alias = CreateUniqueAlias(Path.GetFileName(fullPath)),
            Path = fullPath,
            Kind = ConfigMakerPresetFileKinds.ToResourceKind(kind),
            IsBuiltIn = IsPathInsideComponent(fullPath),
        };
        _document.Resources.Add(resource);
        resourceAdded = true;
        return resource;
    }

    // Replaces the file references of the command and the variables with attached resources.
    // Returns the updated command; the variables are updated in place.
    public string AttachExistingFiles()
    {
        string updatedCommand = _getCommandText();
        foreach (ConfigMakerPresetFileInfo file in ExtractFilesFromText(updatedCommand).ToArray())
        {
            ConfigMakerPresetResource resource = GetOrCreate(file, out bool resourceAdded);
            if (resource == null)
            {
                continue;
            }

            string rewritten = ReplaceFileInText(
                updatedCommand,
                file,
                resource.Reference);
            if (string.Equals(rewritten, updatedCommand, StringComparison.Ordinal))
            {
                if (resourceAdded)
                {
                    _document.Resources.Remove(resource);
                }
                continue;
            }
            updatedCommand = rewritten;
        }

        AttachExistingFilesInVariables();
        return updatedCommand;
    }

    // Points an attached resource at the replacement file. Returns false when the file is not an attached resource.
    public bool TryUpdateAttachedResource(ConfigMakerPresetFileInfo file, string fullReplacementPath)
    {
        ConfigMakerPresetResource attachedResource = FindAttached(file.Path);
        if (attachedResource == null)
        {
            return false;
        }
        attachedResource.Path = fullReplacementPath;
        attachedResource.IsBuiltIn = IsPathInsideComponent(fullReplacementPath);
        return true;
    }

    // Attaches the replacement file and rewrites the references to the replaced file.
    // Returns null when neither the command nor a variable refers to the replaced file.
    public ConfigMakerPresetFileReplacement ReplaceReferences(
        ConfigMakerPresetFileInfo file,
        string fullReplacementPath)
    {
        ConfigMakerPresetResource replacementResource = GetOrCreate(
            fullReplacementPath,
            file.Kind,
            out bool resourceAdded);
        string replacementReference = replacementResource.Reference;
        string commandText = _getCommandText();
        string updatedCommand = ReplaceFileInText(
            commandText,
            file,
            replacementReference);
        bool commandChanged = !string.Equals(
            updatedCommand,
            commandText,
            StringComparison.Ordinal);
        bool variablesChanged = ReplaceFileInVariables(file, replacementReference);
        if (!commandChanged && !variablesChanged)
        {
            if (resourceAdded)
            {
                _document.Resources.Remove(replacementResource);
            }
            return null;
        }
        return new ConfigMakerPresetFileReplacement(replacementReference, updatedCommand, commandChanged);
    }

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

    private bool ReplaceFileInVariables(
        ConfigMakerPresetFileInfo file,
        string replacementPath)
    {
        bool replaced = false;
        foreach (ConfigMakerVariableDefinition variable in _document.Variables)
        {
            variable.Value = ReplaceFileInVariableValue(
                variable.Value,
                file,
                replacementPath,
                ref replaced);
            variable.OnValue = ReplaceFileInVariableValue(
                variable.OnValue,
                file,
                replacementPath,
                ref replaced);
            variable.OffValue = ReplaceFileInVariableValue(
                variable.OffValue,
                file,
                replacementPath,
                ref replaced);
            for (int index = 0; index < variable.Values.Count; index++)
            {
                variable.Values[index] = ReplaceFileInVariableValue(
                    variable.Values[index],
                    file,
                    replacementPath,
                    ref replaced);
            }
        }
        return replaced;
    }

    private string ReplaceFileInVariableValue(
        string value,
        ConfigMakerPresetFileInfo file,
        string replacementPath,
        ref bool replaced)
    {
        string updated = ReplaceFileInText(value ?? string.Empty, file, replacementPath);
        if (!string.Equals(updated, value, StringComparison.Ordinal))
        {
            replaced = true;
            return updated;
        }

        ConfigMakerPresetFileInfo directFile = TryExtractDirectFile(value);
        if (directFile != null &&
            directFile.Kind == file.Kind &&
            ConfigFileReferences.PathsEqual(directFile.Path, file.Path))
        {
            replaced = true;
            return replacementPath;
        }
        return value;
    }

    private void AttachExistingFilesInVariables()
    {
        foreach (ConfigMakerVariableDefinition variable in _document.Variables)
        {
            variable.Value = AttachExistingFilesInVariableValue(variable.Value);
            variable.OnValue = AttachExistingFilesInVariableValue(variable.OnValue);
            variable.OffValue = AttachExistingFilesInVariableValue(variable.OffValue);
            for (int index = 0; index < variable.Values.Count; index++)
            {
                variable.Values[index] = AttachExistingFilesInVariableValue(
                    variable.Values[index]);
            }
        }
    }

    private string AttachExistingFilesInVariableValue(string value)
    {
        string updatedValue = value ?? string.Empty;
        ConfigMakerPresetFileInfo directFile = TryExtractDirectFile(updatedValue);
        ConfigMakerPresetFileInfo[] files = ExtractFilesFromText(updatedValue)
            .Append(directFile)
            .Where(file => file != null)
            .DistinctBy(file => (file.Kind, file.Path), ConfigMakerPresetFileKeyComparer.Instance)
            .ToArray();

        foreach (ConfigMakerPresetFileInfo file in files)
        {
            ConfigMakerPresetResource resource = GetOrCreate(file, out bool resourceAdded);
            if (resource == null)
            {
                continue;
            }

            string rewritten = ReplaceFileInText(updatedValue, file, resource.Reference);
            if (string.Equals(rewritten, updatedValue, StringComparison.Ordinal) &&
                directFile != null &&
                ConfigFileReferences.PathsEqual(directFile.Path, file.Path))
            {
                rewritten = resource.Reference;
            }
            if (string.Equals(rewritten, updatedValue, StringComparison.Ordinal))
            {
                if (resourceAdded)
                {
                    _document.Resources.Remove(resource);
                }
                continue;
            }
            updatedValue = rewritten;
        }
        return updatedValue;
    }
}
