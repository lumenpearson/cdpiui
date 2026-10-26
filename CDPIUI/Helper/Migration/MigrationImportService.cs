using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CDPIUI.Core;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CDPIUI.Helper.Migration;

internal sealed class MigrationImportService
{
    private readonly MigrationArchiveInspectionService _inspectionService = new();

    public static bool IsAlreadyCompleted(VerifiedMigrationPackage package)
    {
        try
        {
            string marker = MigrationImportReportWriter.GetCompletionMarkerPath(package);
            if (!File.Exists(marker) ||
                !Directory.Exists(Path.Combine(
                    Directories.StoreItemsDirectory,
                    SharedConstants.MigratedGoodbyeDpiUiStoreItemId)) ||
                !DatabaseHelper.Instance.IsItemInstalled(SharedConstants.MigratedGoodbyeDpiUiStoreItemId))
                return false;
            JObject data = MigrationPresetConverter.ParseObject(File.ReadAllText(marker));
            return data["migrationId"]?.Value<string>() == package.Request.MigrationId.ToString("D") &&
                data["archiveSha256"]?.Value<string>() == package.Request.ArchiveSha256;
        }
        catch
        {
            return false;
        }
    }

    public MigrationImportResult Import(
        VerifiedMigrationPackage suppliedPackage,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(suppliedPackage);
        progress?.Report(2);
        VerifiedMigrationPackage package = _inspectionService.InspectAndStage(
            suppliedPackage.Request, cancellationToken);
        progress?.Report(12);

        string storeRoot = Path.GetFullPath(Directories.StoreItemsDirectory);
        Directory.CreateDirectory(storeRoot);
        string targetDirectory = Path.Combine(storeRoot, SharedConstants.MigratedGoodbyeDpiUiStoreItemId);
        string stageDirectory = Path.Combine(
            storeRoot,
            $"{SharedConstants.MigratedGoodbyeDpiUiStoreItemId}.importing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stageDirectory);

        string? backupDirectory = null;
        bool stageCommitted = false;
        List<MigrationImportIssue> issues = [];
        try
        {
            MigrationImportBuildResult build = BuildCollection(
                package, stageDirectory, issues, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(72);

            backupDirectory = CommitCollection(stageDirectory, targetDirectory, package);
            stageCommitted = true;
            progress?.Report(82);

            int importedSettings = MigrationSettingsImporter.Apply(
                package.Data, build.PresetFilesBySource, issues, cancellationToken);
            progress?.Report(94);
            MigrationImportReportWriter.WriteReport(targetDirectory, package, build, importedSettings, backupDirectory, issues);
            MigrationImportReportWriter.WriteCompletionMarker(package, build, importedSettings, issues);
            progress?.Report(100);

            return new MigrationImportResult
            {
                ImportedPresetCount = build.PresetCount,
                ImportedResourceCount = build.ResourceCount,
                ImportedSettingCount = importedSettings,
                ReviewRequiredCount = build.ReviewRequiredCount,
                BackupDirectory = backupDirectory,
                Issues = issues
            };
        }
        finally
        {
            if (!stageCommitted && Directory.Exists(stageDirectory))
                DeleteKnownStage(stageDirectory, storeRoot);
        }
    }

    private static MigrationImportBuildResult BuildCollection(
        VerifiedMigrationPackage package,
        string stageDirectory,
        ICollection<MigrationImportIssue> issues,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        CreateCollectionSkeleton(stageDirectory);

        using FileStream archiveStream = new(
            package.StagedArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.SequentialScan);
        using ZipArchive archive = new(archiveStream, ZipArchiveMode.Read);
        Dictionary<string, ZipArchiveEntry> entries = archive.Entries.ToDictionary(
            item => item.FullName, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, MigrationStoredResource> storedResources = new(StringComparer.Ordinal);
        int importedResourceCount = StageResources(
            package, stageDirectory, entries, storedResources, issues, progress, cancellationToken);

        Dictionary<string, string> presetFilesBySource = new(StringComparer.OrdinalIgnoreCase);
        (int importedPresetCount, int reviewCount) = StagePresets(
            package, stageDirectory, entries, storedResources, presetFilesBySource, issues, progress, cancellationToken);

        return new MigrationImportBuildResult(
            importedPresetCount,
            importedResourceCount,
            reviewCount,
            presetFilesBySource);
    }

    private static void CreateCollectionSkeleton(string stageDirectory)
    {
        string listDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemSiteListsFolder);
        string binDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemBinsFolder);
        string localeDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemLocFolder);
        string metadataDirectory = Path.Combine(stageDirectory, "Metadata");
        Directory.CreateDirectory(listDirectory);
        Directory.CreateDirectory(binDirectory);
        Directory.CreateDirectory(localeDirectory);
        Directory.CreateDirectory(metadataDirectory);

        File.WriteAllText(Path.Combine(stageDirectory, "init.json"),
            JsonConvert.SerializeObject(new
            {
                toggleListAvailable = Array.Empty<string>(),
                localized_strings_directory = new Dictionary<string, string>
                {
                    ["EN"] = $"{SharedConstants.LocalUserItemLocFolder}/strings.json"
                }
            }));
        File.WriteAllText(Path.Combine(localeDirectory, "strings.json"), "{}");
    }

    /// <returns>The number of imported resources.</returns>
    private static int StageResources(
        VerifiedMigrationPackage package,
        string stageDirectory,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        Dictionary<string, MigrationStoredResource> storedResources,
        ICollection<MigrationImportIssue> issues,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string listDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemSiteListsFolder);
        string binDirectory = Path.Combine(stageDirectory, SharedConstants.LocalUserItemBinsFolder);
        int resourceIndex = 0;
        int importedResourceCount = 0;
        foreach (MigrationResource resource in package.Data.Resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                bool isBinary = resource.Kind == "binary";
                string extension = isBinary ? ".bin" : ".txt";
                string fileName = resource.Id + extension;
                string destination = Path.Combine(isBinary ? binDirectory : listDirectory, fileName);
                CopyVerifiedEntry(entries[resource.PayloadPath!], destination, resource.Size, resource.Sha256!, cancellationToken);
                string storedReference = string.Join('/',
                    "$GETCURRENTDIR()",
                    isBinary ? SharedConstants.LocalUserItemBinsFolder : SharedConstants.LocalUserItemSiteListsFolder,
                    fileName);
                storedResources.Add(resource.Id!, new MigrationStoredResource(destination, storedReference));
                importedResourceCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                MigrationImportIssueLog.Add(issues, "resource", resource.SourceRelativePath, exception);
            }
            finally
            {
                resourceIndex++;
                progress?.Report(12 + 25d * resourceIndex / Math.Max(1, package.Data.Resources.Count));
            }
        }
        return importedResourceCount;
    }

    private static (int ImportedPresetCount, int ReviewCount) StagePresets(
        VerifiedMigrationPackage package,
        string stageDirectory,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        IReadOnlyDictionary<string, MigrationStoredResource> storedResources,
        Dictionary<string, string> presetFilesBySource,
        ICollection<MigrationImportIssue> issues,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        int presetIndex = 0;
        int importedPresetCount = 0;
        int reviewCount = 0;
        foreach (MigrationPreset preset in package.Data.Presets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                JObject config = MigrationPresetConverter.Convert(entries[preset.PayloadPath!], preset, storedResources);

                string fileName = preset.Id + ".json";
                File.WriteAllText(
                    Path.Combine(stageDirectory, fileName),
                    config.ToString(Formatting.None),
                    new UTF8Encoding(false));
                presetFilesBySource[preset.SourceRelativePath!] = fileName;
                presetFilesBySource[Path.GetFileNameWithoutExtension(preset.SourceRelativePath)] = fileName;
                presetFilesBySource[preset.Name!] = fileName;
                importedPresetCount++;
                if (preset.RequiresReview || preset.Resources.Any(link => !link.IsResolved))
                    reviewCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                MigrationImportIssueLog.Add(issues, "preset", preset.SourceRelativePath ?? preset.Name, exception);
            }
            finally
            {
                presetIndex++;
                progress?.Report(37 + 30d * presetIndex / Math.Max(1, package.Data.Presets.Count));
            }
        }
        return (importedPresetCount, reviewCount);
    }

    private static void CopyVerifiedEntry(
        ZipArchiveEntry entry,
        string destination,
        long expectedSize,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        using Stream source = entry.Open();
        using FileStream target = new(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.WriteThrough);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += read;
            if (total > expectedSize)
                throw new InvalidDataException($"Resource '{entry.FullName}' exceeds its declared size.");
            target.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
        }
        target.Flush(flushToDisk: true);
        string actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (total != expectedSize || actualHash != expectedHash)
            throw new InvalidDataException($"Resource '{entry.FullName}' failed its final integrity check.");
    }

    private static string? CommitCollection(
        string stageDirectory,
        string targetDirectory,
        VerifiedMigrationPackage package)
    {
        string? backupDirectory = null;
        if (Directory.Exists(targetDirectory))
        {
            if ((File.GetAttributes(targetDirectory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The existing migrated preset collection is a reparse point.");
            string backupRoot = Path.Combine(
                Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "Backups");
            Directory.CreateDirectory(backupRoot);
            backupDirectory = Path.Combine(
                backupRoot,
                $"{package.Request.MigrationId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}");
            Directory.Move(targetDirectory, backupDirectory);
        }

        try
        {
            Directory.Move(stageDirectory, targetDirectory);
            DatabaseStoreItem collection = new()
            {
                Id = SharedConstants.MigratedGoodbyeDpiUiStoreItemId,
                Type = "configlist",
                Directory = targetDirectory,
                VersionControlType = "local",
                CurrentVersion = ApplicationInfo.Version,
                IconPath = "$STATICIMAGE(Store/empty.png)",
                Name = "Migrated from GoodbyeDPI-UI",
                ShortName = "Перенесенные из GoodbyeDPI-UI",
                Developer = "GDPIUI-Updater",
                BackgroundColor = string.Empty
            };
            if (!DatabaseHelper.Instance.AddOrUpdateItem(collection))
                throw new InvalidOperationException("CDPIUI could not register the migrated preset collection.");
            return backupDirectory;
        }
        catch
        {
            string failedRoot = Path.Combine(
                Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "FailedImports");
            Directory.CreateDirectory(failedRoot);
            if (Directory.Exists(targetDirectory))
                Directory.Move(targetDirectory, Path.Combine(failedRoot, $"{package.Request.MigrationId:N}-{Guid.NewGuid():N}"));
            if (backupDirectory != null && Directory.Exists(backupDirectory) && !Directory.Exists(targetDirectory))
                Directory.Move(backupDirectory, targetDirectory);
            throw;
        }
    }

    private static void DeleteKnownStage(string stageDirectory, string storeRoot)
    {
        string fullStage = Path.GetFullPath(stageDirectory);
        string prefix = Path.GetFullPath(storeRoot).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullStage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullStage).StartsWith(
                SharedConstants.MigratedGoodbyeDpiUiStoreItemId + ".importing-",
                StringComparison.Ordinal) ||
            (File.GetAttributes(fullStage) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to remove an unexpected import staging directory.");
        Directory.Delete(fullStage, recursive: true);
    }
}
