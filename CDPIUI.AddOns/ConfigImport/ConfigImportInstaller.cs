using System.Text;
using System.Text.RegularExpressions;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Shared;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Installs an analyzed Config into the local user kit. Dependencies are kept in
/// a per-import directory so imports cannot overwrite each other's files.
/// </summary>
public sealed partial class ConfigImportInstaller
{
    public async Task<ConfigImportInstallResult> InstallAsync(
        ConfigImportResult result,
        string displayName)
    {
        if (!result.IsSuccessful || result.Config == null)
            return Failure("IMPORT_RESULT_INVALID");
        if (string.IsNullOrWhiteSpace(displayName))
            return Failure("IMPORT_NAME_REQUIRED");
        if (!HasAllMissingFileResolutions(result))
            return Failure("IMPORT_MISSING_FILE_RESOLUTION_REQUIRED");

        if (result.SharedPackage != null)
            return await InstallSharedPackageAsync(result, displayName);

        string sourceDirectory = Path.GetDirectoryName(result.SourcePath)!;
        string localPackDirectory = ConfigurationService.GetItemFolderFromPackId(SharedConstants.LocalUserItemsId);
        string importId = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..24];
        List<ConfigImportResource> resources = ConfigImportResourcePlanner.Build(result, sourceDirectory, importId);
        List<ImportDirectory> directories = GetImportDirectories(resources, localPackDirectory, importId);

        try
        {
            foreach (ImportDirectory directory in directories)
            {
                Directory.CreateDirectory(directory.Root);
                Directory.CreateDirectory(directory.Staging);
            }

            StageResources(resources, directories);

            ConfigItem config = PrepareConfig(result, displayName, resources);

            foreach (ImportDirectory directory in directories)
                Directory.Move(directory.Staging, directory.Destination);
            string fileName = GetUniqueConfigFileName(localPackDirectory, displayName);
            string errorCode = await ConfigurationService.SaveConfigItem(
                fileName,
                SharedConstants.LocalUserItemsId,
                config);
            if (!string.IsNullOrEmpty(errorCode))
            {
                foreach (ImportDirectory directory in directories)
                    DeleteImportDirectory(directory.Destination, directory.Root);
                DeleteConfigFile(Path.Combine(localPackDirectory, fileName), localPackDirectory);
                return Failure(errorCode);
            }

            return new ConfigImportInstallResult
            {
                ConfigFileName = fileName,
                PackId = SharedConstants.LocalUserItemsId,
                ResourceDirectory = directories.FirstOrDefault()?.Destination,
            };
        }
        catch (Exception exception)
        {
            foreach (ImportDirectory directory in directories)
            {
                DeleteImportDirectory(directory.Staging, directory.Root);
                DeleteImportDirectory(directory.Destination, directory.Root);
            }
            return Failure($"IMPORT_INSTALL_FAILED: {exception.Message}");
        }
    }

    public ConfigItem PrepareConfig(
        ConfigImportResult result,
        string displayName,
        string importId)
    {
        if (!result.IsSuccessful || result.Config == null)
            throw new InvalidOperationException("The import result is not successful.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A Config name is required.", nameof(displayName));
        if (!HasAllMissingFileResolutions(result))
            throw new InvalidOperationException("Every missing file must have an explicit resolution.");

        List<ConfigImportResource> resources = ConfigImportResourcePlanner.Build(
            result,
            Path.GetDirectoryName(result.SourcePath)!,
            importId);
        return PrepareConfig(result, displayName, resources);
    }

    private static async Task<ConfigImportInstallResult> InstallSharedPackageAsync(
        ConfigImportResult result,
        string displayName)
    {
        try
        {
            var installed = await new ConfigShare.ConfigShareService().InstallAsync(
                result.SharedPackage!, displayName, editedConfig: result.Config);
            return new ConfigImportInstallResult { ConfigFileName = installed.ConfigFileName, PackId = installed.PackId };
        }
        catch (ConfigShare.ConfigShareException exception)
        {
            return Failure($"{exception.Code}: {exception.Message}");
        }
    }

    private static List<ImportDirectory> GetImportDirectories(
        IReadOnlyList<ConfigImportResource> resources,
        string localPackDirectory,
        string importId) =>
        resources
            .Select(resource => resource.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(category => new ImportDirectory(
                category,
                Path.Combine(localPackDirectory, category, ConfigImportResourcePlanner.ConvertedDirectoryName),
                Path.Combine(localPackDirectory, category, ConfigImportResourcePlanner.ConvertedDirectoryName, $".{importId}.tmp"),
                Path.Combine(localPackDirectory, category, ConfigImportResourcePlanner.ConvertedDirectoryName, importId)))
            .ToList();

    private static void StageResources(
        IReadOnlyList<ConfigImportResource> resources,
        IReadOnlyList<ImportDirectory> directories)
    {
        foreach (ConfigImportResource resource in resources)
        {
            ImportDirectory directory = directories.First(item =>
                item.Category.Equals(resource.Category, StringComparison.OrdinalIgnoreCase));
            string destinationPath = GetSafeDestination(directory.Staging, resource.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            if (resource.GeneratedContent != null)
            {
                File.WriteAllText(destinationPath, resource.GeneratedContent, new UTF8Encoding(false));
            }
            else if (!string.IsNullOrWhiteSpace(resource.SourcePath))
            {
                File.Copy(resource.SourcePath, destinationPath, overwrite: false);
            }
            else
            {
                File.WriteAllBytes(destinationPath, []);
            }
        }
    }

    private static ConfigItem PrepareConfig(
        ConfigImportResult result,
        string displayName,
        IReadOnlyList<ConfigImportResource> resources)
    {
        ConfigItem config = CloneConfig(result.Config!);
        config.name = displayName.Trim();
        config.not_converted_name = config.name;
        ConfigImportPathRewriter.RewriteResourcePaths(config, resources);
        return config;
    }

    private static ConfigItem CloneConfig(ConfigItem config) =>
        JSONConvertor.DeserializeObject<ConfigItem>(JSONConvertor.SerializeObject(config))
        ?? throw new InvalidOperationException("Cannot clone imported Config.");

    private static string GetSafeDestination(string root, string relativePath)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string destination = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsafe imported file path: {relativePath}");
        return destination;
    }

    private static string GetUniqueConfigFileName(string localPackDirectory, string displayName)
    {
        string safeName = InvalidFileNameRegex().Replace(displayName.Trim(), "_");
        safeName = WhitespaceRegex().Replace(safeName, "_").Trim('_', '.');
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "Imported_Config";

        string candidate = $"{safeName}.json";
        for (int suffix = 2; File.Exists(Path.Combine(localPackDirectory, candidate)); suffix++)
            candidate = $"{safeName}_{suffix}.json";
        return candidate;
    }

    private static bool HasAllMissingFileResolutions(ConfigImportResult result)
    {
        var resolvedPaths = result.MissingFileResolutions
            .Select(resolution => Path.GetFullPath(resolution.MissingPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return result.MissingReferencedFiles.All(path =>
            resolvedPaths.Contains(Path.GetFullPath(path)));
    }

    private static void DeleteImportDirectory(string path, string resourcesRoot)
    {
        try
        {
            string fullRoot = Path.GetFullPath(resourcesRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
        catch (Exception ex)
        {
            // Best-effort rollback; the original error is more useful to the caller.
            Logger.Instance.CreateWarningLog(nameof(ConfigImportInstaller), $"Cannot remove the import directory \"{path}\": {ex.Message}");
        }
    }

    private static void DeleteConfigFile(string path, string localPackDirectory)
    {
        try
        {
            string fullRoot = Path.GetFullPath(localPackDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            // Best-effort rollback.
            Logger.Instance.CreateWarningLog(nameof(ConfigImportInstaller), $"Cannot remove the config file \"{path}\": {ex.Message}");
        }
    }

    private static ConfigImportInstallResult Failure(string errorCode) => new() { ErrorCode = errorCode };

    [GeneratedRegex(@"[^\p{L}\p{Nd}_.-]")]
    private static partial Regex InvalidFileNameRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record ImportDirectory(
        string Category,
        string Root,
        string Staging,
        string Destination);
}
