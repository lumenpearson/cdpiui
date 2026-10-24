#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ConfigMakerPresetStorageService
{
    public Task<ConfigMakerPresetSaveResult> SaveAsync(
        string presetName,
        ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ConfigMakerPresetDocument snapshot = document.Clone();
        return Task.Run(() => SaveCoreAsync(presetName, snapshot, overwrite: false));
    }

    public Task<ConfigMakerPresetSaveResult> OverwriteAsync(
        ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ConfigMakerPresetDocument snapshot = document.Clone();
        return Task.Run(() => SaveCoreAsync(snapshot.Name, snapshot, overwrite: true));
    }

    private static async Task<ConfigMakerPresetSaveResult> SaveCoreAsync(
        string presetName,
        ConfigMakerPresetDocument document,
        bool overwrite)
    {
        string normalizedName = ConfigMakerPresetStorageNaming.NormalizeDisplayName(presetName);
        ConfigMakerPresetSaveResult? invalidInput = ValidateInput(normalizedName, document);
        if (invalidInput != null)
        {
            return invalidInput;
        }
        if (!TryGetDestination(document, overwrite, out string destinationPackId, out string configFileName))
        {
            return ConfigMakerPresetSaveResult.Failed("OVERWRITE_TARGET_MISSING");
        }

        string componentDirectory = DatabaseHelper.Instance.GetItemById(document.ComponentId)?.Directory ?? string.Empty;
        if (string.IsNullOrWhiteSpace(componentDirectory) || !Directory.Exists(componentDirectory))
        {
            return ConfigMakerPresetSaveResult.Failed("COMPONENT_UNAVAILABLE");
        }

        string userStorage = Directories.StoreLocalUserItemDirectory;
        Directory.CreateDirectory(userStorage);
        string destinationPresetDirectory = ConfigurationService.GetItemFolderFromPackId(destinationPackId);
        if (overwrite && !Directory.Exists(destinationPresetDirectory))
        {
            return ConfigMakerPresetSaveResult.Failed("PACK_UNAVAILABLE", destinationPackId);
        }
        Directory.CreateDirectory(destinationPresetDirectory);

        string sourcePresetDirectory = ConfigMakerPresetPaths.GetSourcePresetDirectory(document, userStorage);
        string storageId = overwrite
            ? ConfigMakerPresetStorageNaming.CreateOverwriteStorageId(configFileName)
            : ConfigMakerPresetStorageNaming.CreateStorageId(normalizedName, destinationPresetDirectory);
        if (!overwrite)
        {
            configFileName = $"{storageId}.json";
        }
        ConfigMakerPresetResourceCopier copier = new(
            componentDirectory,
            destinationPresetDirectory,
            storageId);

        try
        {
            Dictionary<string, string> replacements = new(StringComparer.OrdinalIgnoreCase);
            List<ConfigMakerResourceMetadata> storedResources = [];
            ConfigItem sourceConfig = document.ToConfigItem(
                document.PackId ?? destinationPackId,
                normalizedName);

            foreach (ConfigMakerPresetResource resource in document.Resources)
            {
                string? sourcePath = ConfigMakerPresetPaths.ResolveResourcePath(
                    resource.Path,
                    componentDirectory,
                    sourcePresetDirectory,
                    sourceConfig);
                if (sourcePath == null)
                {
                    copier.Cleanup();
                    return ConfigMakerPresetSaveResult.Failed("FILE_MISSING", resource.Path);
                }

                string storedReference = copier.Store(
                    resource,
                    sourcePath,
                    out bool keepComponentReference);
                replacements[resource.Alias] = storedReference;
                storedResources.Add(new ConfigMakerResourceMetadata
                {
                    alias = resource.Alias,
                    path = storedReference,
                    kind = resource.Kind.ToString(),
                    isBuiltIn = keepComponentReference,
                });
            }

            ConfigItem config = CreateStoredConfig(
                document,
                destinationPackId,
                normalizedName,
                replacements,
                storedResources);
            ConfigUsedFile? missingFile = FindMissingFile(
                config,
                componentDirectory,
                destinationPresetDirectory);
            if (missingFile != null)
            {
                copier.Cleanup();
                return ConfigMakerPresetSaveResult.Failed("FILE_MISSING", missingFile.ExpandedPath);
            }

            string errorCode = await ConfigurationService.SaveConfigItem(
                configFileName,
                destinationPackId,
                config);
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                copier.Cleanup();
                return ConfigMakerPresetSaveResult.Failed("SAVE_FAILED", errorCode);
            }

            TrySaveVariableDescriptions(document, destinationPackId);
            TryReloadConfigs(document.ComponentId);

            return new ConfigMakerPresetSaveResult
            {
                Success = true,
                PackId = destinationPackId,
                ConfigFileName = configFileName,
                CopiedFileCount = copier.CopiedFileCount,
                StoredResources = storedResources,
            };
        }
        catch (Exception exception)
        {
            copier.Cleanup();
            return ConfigMakerPresetSaveResult.Failed("UNEXPECTED", exception.Message);
        }
    }

    public static string CreateResolvedCommandForTest(ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Dictionary<string, string> replacements = CreateResourceReplacements(
            document,
            keepPortableComponentPaths: false);

        ConfigItem runtimeConfig = document.ToConfigItem(
            SharedConstants.LocalUserItemsId,
            document.Name);
        runtimeConfig.RewritePresetReferences(replacements);
        return ConfigurationService.GetStartupParametersByConfigItem(runtimeConfig);
    }

    public static string CreateResolvedCommandForTextExport(ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.HasVariables)
        {
            throw new InvalidOperationException("VARIABLES_NOT_SUPPORTED");
        }
        Dictionary<string, string> replacements = CreateResourceReplacements(
            document,
            keepPortableComponentPaths: true);
        return RewritePresetReferences(document.CommandText, replacements);
    }

    public static string RewritePresetReferences(
        string commandText,
        IReadOnlyDictionary<string, string> replacements) =>
        ConfigFileReferences.RewritePresetReferences(commandText, replacements);

    private static ConfigMakerPresetSaveResult? ValidateInput(
        string normalizedName,
        ConfigMakerPresetDocument document)
    {
        if (normalizedName.Length == 0)
        {
            return ConfigMakerPresetSaveResult.Failed("NAME_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(document.ComponentId))
        {
            return ConfigMakerPresetSaveResult.Failed("COMPONENT_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(document.CommandText))
        {
            return ConfigMakerPresetSaveResult.Failed("COMMAND_EMPTY");
        }
        return null;
    }

    private static bool TryGetDestination(
        ConfigMakerPresetDocument document,
        bool overwrite,
        out string destinationPackId,
        out string configFileName)
    {
        if (!overwrite)
        {
            destinationPackId = SharedConstants.LocalUserItemsId;
            configFileName = string.Empty;
            return true;
        }

        destinationPackId = document.PackId?.Trim() ?? string.Empty;
        configFileName = document.FileName?.Trim() ?? string.Empty;
        return destinationPackId.Length != 0 &&
            configFileName.Length != 0 &&
            string.Equals(configFileName, Path.GetFileName(configFileName), StringComparison.Ordinal);
    }

    private static ConfigItem CreateStoredConfig(
        ConfigMakerPresetDocument document,
        string destinationPackId,
        string normalizedName,
        IReadOnlyDictionary<string, string> replacements,
        List<ConfigMakerResourceMetadata> storedResources)
    {
        ConfigItem config = document.ToConfigItem(
            destinationPackId,
            normalizedName);
        string? targetVersion = !string.IsNullOrWhiteSpace(document.TargetVersion)
            ? document.TargetVersion
            : DatabaseHelper.Instance.GetItemById(document.ComponentId)?.CurrentVersion;
        config.target =
        [
            document.ComponentId,
            string.IsNullOrWhiteSpace(targetVersion) ? "%CURRENT%" : targetVersion,
        ];
        config.RewritePresetReferences(replacements);
        if (config.configMaker != null || storedResources.Count > 0)
        {
            config.configMaker ??= new ConfigMakerPresetMetadata();
            config.configMaker.resources = storedResources.Count == 0 ? null : storedResources;
        }
        config.NormalizeForStorage();
        return config;
    }

    private static ConfigUsedFile? FindMissingFile(
        ConfigItem config,
        string componentDirectory,
        string destinationPresetDirectory) =>
        config.UsedFiles.FirstOrDefault(file =>
        {
            try
            {
                return !File.Exists(config.ResolveFilePath(
                    file.Path,
                    componentDirectory,
                    destinationPresetDirectory));
            }
            catch
            {
                return true;
            }
        });

    private static void TryReloadConfigs(string componentId)
    {
        try
        {
            ComponentItemsLoaderHelper.Instance
                .GetComponentHelperFromId(componentId)?
                .ReInitConfigs();
        }
        catch (Exception ex)
        {
            // The config and its resources are already safely stored. A later reload will pick them up.
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerPresetStorageService), $"Cannot reload the configs: {ex.Message}");
        }
    }

    private static Dictionary<string, string> CreateResourceReplacements(
        ConfigMakerPresetDocument document,
        bool keepPortableComponentPaths)
    {
        string componentDirectory = DatabaseHelper.Instance.GetItemById(document.ComponentId)?.Directory ?? string.Empty;
        string userStorage = Directories.StoreLocalUserItemDirectory;
        string presetDirectory = ConfigMakerPresetPaths.GetSourcePresetDirectory(document, userStorage);
        ConfigItem sourceConfig = document.ToConfigItem(
            document.PackId ?? SharedConstants.LocalUserItemsId,
            document.Name);
        Dictionary<string, string> replacements = new(StringComparer.OrdinalIgnoreCase);
        foreach (ConfigMakerPresetResource resource in document.Resources)
        {
            string? fullPath = ConfigMakerPresetPaths.ResolveResourcePath(
                resource.Path,
                componentDirectory,
                presetDirectory,
                sourceConfig);
            if (fullPath == null)
            {
                throw new FileNotFoundException(null, resource.Path);
            }
            replacements[resource.Alias] = keepPortableComponentPaths &&
                resource.IsBuiltIn &&
                !string.IsNullOrWhiteSpace(componentDirectory) &&
                ConfigMakerPresetPaths.IsPathInside(fullPath, componentDirectory)
                    ? "/" + Path.GetRelativePath(componentDirectory, fullPath).Replace('\\', '/')
                    : fullPath;
        }
        return replacements;
    }

    private static void TrySaveVariableDescriptions(
        ConfigMakerPresetDocument document,
        string packId)
    {
        try
        {
            string localePath = ConfigurationService.GetDefaultLocalePath(packId);
            if (string.IsNullOrWhiteSpace(localePath))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(localePath)!);
            Dictionary<string, string> values = File.Exists(localePath)
                ? JSONConvertor.LoadJson<Dictionary<string, string>>(localePath) ?? []
                : [];
            bool changed = false;
            foreach (ConfigMakerVariableDefinition variable in document.Variables.Where(variable =>
                         variable.Kind == ConfigMakerVariableKind.Switch &&
                         !string.IsNullOrWhiteSpace(variable.InternalParameterName)))
            {
                values[variable.InternalParameterName] = string.IsNullOrWhiteSpace(variable.Description)
                    ? variable.Name
                    : variable.Description;
                changed = true;
            }
            if (changed)
            {
                File.WriteAllText(localePath, System.Text.Json.JsonSerializer.Serialize(values));
            }
        }
        catch (Exception ex)
        {
            // Variable localization is editor metadata. It must not make the preset unusable.
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerPresetStorageService), $"Cannot save variable descriptions: {ex.Message}");
        }
    }
}
