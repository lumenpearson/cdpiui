using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using static CDPIUI.Helper.AddOns.BlockCheck2.BlockCheck2PresetStorageNames;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

public sealed class BlockCheck2PresetStorageService
{
    public Task<BlockCheck2PresetStorageResult> SaveAsync(
        string presetName,
        BlockCheck2PresetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        PresetSnapshot snapshot = new(
            draft.EffectiveArguments,
            draft.CanUseConfig,
            draft.Files.Select(file => new PresetFileSnapshot(file.Path, file.Kind)).ToArray());
        return Task.Run(() => SaveCoreAsync(presetName, snapshot));
    }

    private static async Task<BlockCheck2PresetStorageResult> SaveCoreAsync(
        string presetName,
        PresetSnapshot draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        string normalizedName = (presetName ?? string.Empty).Trim();
        if (normalizedName.Length == 0)
        {
            return BlockCheck2PresetStorageResult.Failed("NAME_EMPTY");
        }
        if (!draft.CanUseConfig)
        {
            return BlockCheck2PresetStorageResult.Failed("PRESET_EMPTY");
        }

        string componentId = HardcodedItemIds.ComponentIds[Components.Zapret2];
        string componentDirectory = GetComponentDirectory(componentId);
        if (string.IsNullOrWhiteSpace(componentDirectory) || !Directory.Exists(componentDirectory))
        {
            return BlockCheck2PresetStorageResult.Failed("COMPONENT_UNAVAILABLE");
        }

        string userStorage = Directories.StoreLocalUserItemDirectory;
        Directory.CreateDirectory(userStorage);
        StorageTarget target = CreateStorageTarget(normalizedName, userStorage);
        List<string> createdRoots = [];

        try
        {
            FileStagingResult staging = StageFiles(
                draft.Files,
                componentDirectory,
                userStorage,
                target,
                createdRoots);
            if (staging.HasMissingFile)
            {
                Cleanup(createdRoots);
                return BlockCheck2PresetStorageResult.Failed(
                    "FILE_MISSING",
                    staging.MissingReference);
            }

            string startupArguments = BlockCheck2PresetReferenceRewriter.RewriteFileReferences(
                draft.EffectiveArguments,
                staging.Replacements);
            string configFileName = $"{target.StorageId}.json";
            string errorCode = await SaveConfigAsync(
                normalizedName,
                componentId,
                configFileName,
                startupArguments).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                Cleanup(createdRoots);
                return BlockCheck2PresetStorageResult.Failed("SAVE_FAILED", errorCode);
            }

            return new BlockCheck2PresetStorageResult
            {
                Success = true,
                PresetName = normalizedName,
                ConfigFileName = configFileName,
                CopiedFileCount = staging.CopiedFileCount,
            };
        }
        catch (Exception exception)
        {
            Cleanup(createdRoots);
            return BlockCheck2PresetStorageResult.Failed(
                "UNEXPECTED",
                exception.Message);
        }
    }

    private static string GetComponentDirectory(string componentId)
    {
        ComponentHelper component = ComponentItemsLoaderHelper.Instance
            .GetComponentHelperFromId(componentId);
        string executablePath = component?.GetExecutablePath();
        return string.IsNullOrWhiteSpace(executablePath)
            ? null
            : Path.GetDirectoryName(executablePath);
    }

    private static StorageTarget CreateStorageTarget(string normalizedName, string userStorage)
    {
        string storageId = CreateStorageId(normalizedName, userStorage);
        string listRoot = Path.Combine(
            userStorage,
            SharedConstants.LocalUserItemSiteListsFolder,
            StorageFolderName,
            storageId);
        string binRoot = Path.Combine(
            userStorage,
            SharedConstants.LocalUserItemBinsFolder,
            StorageFolderName,
            storageId);
        return new StorageTarget(storageId, listRoot, binRoot);
    }

    /// <summary>
    /// Copies the preset files that live outside the component folder into the storage
    /// folders and collects the references that replace them. Stops at the first file
    /// that cannot be found and reports it through <see cref="FileStagingResult.HasMissingFile"/>.
    /// </summary>
    private static FileStagingResult StageFiles(
        IReadOnlyList<PresetFileSnapshot> files,
        string componentDirectory,
        string userStorage,
        StorageTarget target,
        List<string> createdRoots)
    {
        List<BlockCheck2PresetFileReplacement> replacements = [];
        Dictionary<string, string> copiedFiles = new(StringComparer.OrdinalIgnoreCase);
        int copiedFileCount = 0;

        foreach (PresetFileSnapshot file in files)
        {
            string sourcePath = ResolveSourcePath(
                file.Path,
                componentDirectory,
                userStorage);
            if (sourcePath == null)
            {
                return new FileStagingResult(replacements, copiedFileCount, true, file.Path);
            }

            if (file.Kind != BlockCheck2PresetFileKind.SiteList &&
                IsPathInside(sourcePath, componentDirectory))
            {
                string componentRelativePath = Path.GetRelativePath(
                        componentDirectory,
                        sourcePath)
                    .Replace('\\', '/');
                replacements.Add(new BlockCheck2PresetFileReplacement(
                    file.Kind,
                    file.Path,
                    componentRelativePath));
                continue;
            }

            string destinationPath = CopyToStorage(
                file,
                sourcePath,
                target,
                copiedFiles,
                createdRoots,
                ref copiedFileCount);
            replacements.Add(new BlockCheck2PresetFileReplacement(
                file.Kind,
                file.Path,
                CreateStoredReference(file.Kind, target.StorageId, destinationPath)));
        }

        return new FileStagingResult(replacements, copiedFileCount, false, null);
    }

    private static string CopyToStorage(
        PresetFileSnapshot file,
        string sourcePath,
        StorageTarget target,
        Dictionary<string, string> copiedFiles,
        List<string> createdRoots,
        ref int copiedFileCount)
    {
        string destinationRoot = file.Kind == BlockCheck2PresetFileKind.SiteList
            ? target.ListRoot
            : target.BinRoot;
        if (!Directory.Exists(destinationRoot))
        {
            Directory.CreateDirectory(destinationRoot);
            createdRoots.Add(destinationRoot);
        }

        string copyKey = $"{file.Kind}|{sourcePath}";
        if (!copiedFiles.TryGetValue(copyKey, out string destinationPath))
        {
            destinationPath = GetUniqueDestinationPath(
                destinationRoot,
                Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destinationPath, overwrite: false);
            copiedFiles[copyKey] = destinationPath;
            copiedFileCount++;
        }
        return destinationPath;
    }

    private static string CreateStoredReference(
        BlockCheck2PresetFileKind kind,
        string storageId,
        string destinationPath)
    {
        string category = kind == BlockCheck2PresetFileKind.SiteList
            ? SharedConstants.LocalUserItemSiteListsFolder
            : SharedConstants.LocalUserItemBinsFolder;
        return string.Join('/',
            "$GETCURRENTDIR()",
            category,
            StorageFolderName,
            storageId,
            Path.GetFileName(destinationPath));
    }

    private static Task<string> SaveConfigAsync(
        string normalizedName,
        string componentId,
        string configFileName,
        string startupArguments)
    {
        ConfigItem config = CreateConfigPageHelper.CreateConfigItem(
            SharedConstants.LocalUserItemsId,
            normalizedName,
            componentId,
            [],
            [],
            [],
            [],
            startupArguments);
        return ConfigurationService.SaveConfigItem(
            configFileName,
            SharedConstants.LocalUserItemsId,
            config);
    }

    private static string ResolveSourcePath(
        string reference,
        string componentDirectory,
        string userStorage)
    {
        string normalized = BlockCheck2PresetReferenceRewriter.NormalizeReference(reference);
        if (normalized.StartsWith("GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = normalized["GETCURRENTDIR()".Length..]
                .TrimStart('/', '\\');
            string expanded = Path.GetFullPath(Path.Combine(
                userStorage,
                suffix.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(expanded) ? expanded : null;
        }

        string value = Environment.ExpandEnvironmentVariables(normalized)
            .Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(value))
        {
            string fullPath = Path.GetFullPath(value);
            return File.Exists(fullPath) ? fullPath : null;
        }

        string[] candidates =
        [
            Path.Combine(componentDirectory, value),
            Path.Combine(Directories.CurrentDirectory, value),
            Path.Combine(userStorage, value),
        ];
        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    private static bool IsPathInside(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(
                   fullRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void Cleanup(IEnumerable<string> roots)
    {
        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception ex)
            {
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(BlockCheck2PresetStorageService), $"Cannot remove \"{root}\": {ex.Message}");
            }
        }
    }

    private sealed record StorageTarget(
        string StorageId,
        string ListRoot,
        string BinRoot);

    private sealed record FileStagingResult(
        IReadOnlyList<BlockCheck2PresetFileReplacement> Replacements,
        int CopiedFileCount,
        bool HasMissingFile,
        string MissingReference);

    private sealed record PresetFileSnapshot(
        string Path,
        BlockCheck2PresetFileKind Kind);

    private sealed record PresetSnapshot(
        string EffectiveArguments,
        bool CanUseConfig,
        IReadOnlyList<PresetFileSnapshot> Files);
}
