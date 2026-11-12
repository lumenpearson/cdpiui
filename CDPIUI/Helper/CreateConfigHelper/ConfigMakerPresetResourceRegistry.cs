#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.ViewModels;
using Unidecode.NET;

namespace CDPIUI.Helper.CreateConfigHelper;

// Owns attached resource identity and registration; path policy belongs to the caller.
internal sealed class ConfigMakerPresetResourceRegistry
{
    private const string PresetScheme = "preset://";
    private readonly IList<ConfigMakerPresetResource> _resources;
    private readonly Func<string, string> _resolvePath;
    private readonly Func<string, bool> _isPathInsideComponent;

    public ConfigMakerPresetResourceRegistry(
        IList<ConfigMakerPresetResource> resources,
        Func<string, string> resolvePath,
        Func<string, bool> isPathInsideComponent)
    {
        _resources = resources;
        _resolvePath = resolvePath;
        _isPathInsideComponent = isPathInsideComponent;
    }

    public ConfigMakerPresetResource? FindAttached(string? reference)
    {
        string alias = (reference ?? string.Empty).Trim().Trim('"', '\'');
        if (alias.StartsWith(PresetScheme, StringComparison.OrdinalIgnoreCase))
        {
            alias = alias[PresetScheme.Length..];
        }
        return _resources.FirstOrDefault(resource =>
            string.Equals(resource.Alias, alias, StringComparison.OrdinalIgnoreCase));
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
        while (_resources.Any(resource =>
                   string.Equals(resource.Alias, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{stem}_{suffix++}{extension}";
        }
        return candidate;
    }

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
            if (_resources.Any(resource =>
                    string.Equals(
                        _resolvePath(resource.Reference),
                        fullPath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            string alias = CreateUniqueAlias(Path.GetFileName(fullPath));
            _resources.Add(new ConfigMakerPresetResource
            {
                Alias = alias,
                Path = fullPath,
                Kind = ConfigMakerPresetFileKinds.InferResourceKind(fullPath),
                IsBuiltIn = _isPathInsideComponent(fullPath),
            });
            added++;
        }
        return added;
    }

    public ConfigMakerPresetResource? GetOrCreate(
        ConfigMakerPresetFileInfo? file,
        out bool resourceAdded)
    {
        resourceAdded = false;
        if (file == null || file.Path.StartsWith(PresetScheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string resolvedPath = _resolvePath(file.Path);
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
        ConfigMakerPresetResource? resource = _resources.FirstOrDefault(candidate =>
            string.Equals(
                _resolvePath(candidate.Reference),
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
            IsBuiltIn = _isPathInsideComponent(fullPath),
        };
        _resources.Add(resource);
        resourceAdded = true;
        return resource;
    }

    public bool TryUpdateAttachedResource(ConfigMakerPresetFileInfo file, string fullReplacementPath)
    {
        ConfigMakerPresetResource? attachedResource = FindAttached(file.Path);
        if (attachedResource == null)
        {
            return false;
        }
        attachedResource.Path = fullReplacementPath;
        attachedResource.IsBuiltIn = _isPathInsideComponent(fullReplacementPath);
        return true;
    }

    public bool Remove(ConfigMakerPresetResource resource) => _resources.Remove(resource);
}
