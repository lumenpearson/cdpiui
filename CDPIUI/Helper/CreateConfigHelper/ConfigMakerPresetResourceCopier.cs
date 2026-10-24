#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDPIUI.Shared;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Stores the resources of one preset save and removes what it created when the save fails.
internal sealed class ConfigMakerPresetResourceCopier
{
    private readonly string _componentDirectory;
    private readonly string _destinationPresetDirectory;
    private readonly string _storageId;
    private readonly string _listRoot;
    private readonly string _binRoot;
    private readonly List<string> _createdRoots = [];
    private readonly List<string> _createdFiles = [];
    private readonly Dictionary<string, string> _copiedFiles = new(StringComparer.OrdinalIgnoreCase);

    public ConfigMakerPresetResourceCopier(
        string componentDirectory,
        string destinationPresetDirectory,
        string storageId)
    {
        _componentDirectory = componentDirectory;
        _destinationPresetDirectory = destinationPresetDirectory;
        _storageId = storageId;
        _listRoot = Path.Combine(
            destinationPresetDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            ConfigMakerPresetStorageNaming.StorageFolderName,
            storageId);
        _binRoot = Path.Combine(
            destinationPresetDirectory,
            SharedConstants.LocalUserItemBinsFolder,
            ConfigMakerPresetStorageNaming.StorageFolderName,
            storageId);
    }

    public int CopiedFileCount { get; private set; }

    // Returns the reference the stored preset uses for the resource.
    // keepComponentReference is true when the reference points into the component directory.
    public string Store(
        ConfigMakerPresetResource resource,
        string sourcePath,
        out bool keepComponentReference)
    {
        string storedReference;
        keepComponentReference = resource.IsBuiltIn &&
            ConfigMakerPresetPaths.IsPathInside(sourcePath, _componentDirectory);
        if (keepComponentReference)
        {
            storedReference = "/" + Path.GetRelativePath(_componentDirectory, sourcePath).Replace('\\', '/');
        }
        else if (ConfigMakerPresetPaths.IsPathInside(sourcePath, _destinationPresetDirectory))
        {
            storedReference = string.Join(
                '/',
                "$GETCURRENTDIR()",
                Path.GetRelativePath(_destinationPresetDirectory, sourcePath).Replace('\\', '/'));
        }
        else
        {
            storedReference = CopyIntoPreset(resource, sourcePath);
        }

        return storedReference;
    }

    public void Cleanup()
    {
        foreach (string file in _createdFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception exception)
            {
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerPresetStorageService), exception.ToString());
            }
        }
        foreach (string root in _createdRoots.OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception exception)
            {
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerPresetStorageService), exception.ToString());
            }
        }
    }

    private string CopyIntoPreset(ConfigMakerPresetResource resource, string sourcePath)
    {
        string destinationRoot = resource.Kind == ConfigMakerResourceKind.SiteList
            ? _listRoot
            : _binRoot;
        if (!Directory.Exists(destinationRoot))
        {
            Directory.CreateDirectory(destinationRoot);
            _createdRoots.Add(destinationRoot);
        }
        string copyKey = $"{resource.Kind}|{sourcePath}";
        if (!_copiedFiles.TryGetValue(copyKey, out string? destinationPath))
        {
            destinationPath = ConfigMakerPresetStorageNaming.GetUniqueDestinationPath(
                destinationRoot,
                Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destinationPath, overwrite: false);
            _copiedFiles[copyKey] = destinationPath;
            _createdFiles.Add(destinationPath);
            CopiedFileCount++;
        }
        string category = resource.Kind == ConfigMakerResourceKind.SiteList
            ? SharedConstants.LocalUserItemSiteListsFolder
            : SharedConstants.LocalUserItemBinsFolder;
        return string.Join(
            '/',
            "$GETCURRENTDIR()",
            category,
            ConfigMakerPresetStorageNaming.StorageFolderName,
            _storageId,
            Path.GetFileName(destinationPath));
    }
}
