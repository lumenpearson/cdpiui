#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Rewrites command and variable references, retaining only resources used by a successful rewrite.
internal sealed class ConfigMakerPresetReferenceEditor
{
    private readonly IEnumerable<ConfigMakerVariableDefinition> _variables;
    private readonly ConfigMakerPresetResourceRegistry _registry;
    private readonly Func<string, IEnumerable<ConfigMakerPresetFileInfo>> _extractFiles;
    private readonly Func<string?, ConfigMakerPresetFileInfo?> _extractDirectFile;

    public ConfigMakerPresetReferenceEditor(
        IEnumerable<ConfigMakerVariableDefinition> variables,
        ConfigMakerPresetResourceRegistry registry,
        Func<string, IEnumerable<ConfigMakerPresetFileInfo>> extractFiles,
        Func<string?, ConfigMakerPresetFileInfo?> extractDirectFile)
    {
        _variables = variables;
        _registry = registry;
        _extractFiles = extractFiles;
        _extractDirectFile = extractDirectFile;
    }

    public string AttachExistingFiles(string commandText)
    {
        string updatedCommand = commandText;
        foreach (ConfigMakerPresetFileInfo file in _extractFiles(updatedCommand).ToArray())
        {
            ConfigMakerPresetResource? resource = _registry.GetOrCreate(file, out bool resourceAdded);
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
                    _registry.Remove(resource);
                }
                continue;
            }
            updatedCommand = rewritten;
        }

        AttachExistingFilesInVariables();
        return updatedCommand;
    }

    public ConfigMakerPresetFileReplacement? ReplaceReferences(
        Func<string> getCommandText,
        ConfigMakerPresetFileInfo file,
        string fullReplacementPath)
    {
        ConfigMakerPresetResource replacementResource = _registry.GetOrCreate(
            fullReplacementPath,
            file.Kind,
            out bool resourceAdded);
        string replacementReference = replacementResource.Reference;
        // Resource notifications may synchronously update the document/editor text.
        string commandText = getCommandText();
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
                _registry.Remove(replacementResource);
            }
            return null;
        }
        return new ConfigMakerPresetFileReplacement(replacementReference, updatedCommand, commandChanged);
    }

    private static string ReplaceFileInText(
        string text,
        ConfigMakerPresetFileInfo file,
        string replacementPath) =>
        ConfigFileReferences.ReplaceFileInText(
            text,
            file.Path,
            ConfigMakerPresetFileKinds.ToCoreFileKind(file.Kind),
            replacementPath);

    private bool ReplaceFileInVariables(
        ConfigMakerPresetFileInfo file,
        string replacementPath)
    {
        bool replaced = false;
        foreach (ConfigMakerVariableDefinition variable in _variables)
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
        string? value,
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

        ConfigMakerPresetFileInfo? directFile = _extractDirectFile(value);
        if (directFile != null &&
            directFile.Kind == file.Kind &&
            ConfigFileReferences.PathsEqual(directFile.Path, file.Path))
        {
            replaced = true;
            return replacementPath;
        }
        return value!;
    }

    private void AttachExistingFilesInVariables()
    {
        foreach (ConfigMakerVariableDefinition variable in _variables)
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
        ConfigMakerPresetFileInfo? directFile = _extractDirectFile(updatedValue);
        ConfigMakerPresetFileInfo[] files = _extractFiles(updatedValue)
            .Append(directFile)
            .OfType<ConfigMakerPresetFileInfo>()
            .DistinctBy(file => (file.Kind, file.Path), ConfigMakerPresetFileKeyComparer.Instance)
            .ToArray();

        foreach (ConfigMakerPresetFileInfo file in files)
        {
            ConfigMakerPresetResource? resource = _registry.GetOrCreate(file, out bool resourceAdded);
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
                    _registry.Remove(resource);
                }
                continue;
            }
            updatedValue = rewritten;
        }
        return updatedValue;
    }
}
