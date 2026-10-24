using System;
using System.Collections.Generic;
using System.IO;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using Newtonsoft.Json;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// Writes the migration report next to the imported presets and the marker that a
/// migration was completed.
/// </summary>
internal static class MigrationImportReportWriter
{
    internal static string GetCompletionMarkerPath(VerifiedMigrationPackage package) =>
        Path.Combine(
            Directories.DataDirectory,
            "Migration", "GoodbyeDPIUI", "Completed",
            package.Request.MigrationId.ToString("N") + ".json");

    internal static void WriteReport(
        string targetDirectory,
        VerifiedMigrationPackage package,
        MigrationImportBuildResult build,
        int importedSettings,
        string? backupDirectory,
        IReadOnlyCollection<MigrationImportIssue> issues)
    {
        try
        {
            string reportPath = Path.Combine(targetDirectory, "Metadata", "migration-report.json");
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(new
            {
                schemaVersion = 1,
                migrationId = package.Request.MigrationId.ToString("D"),
                archiveSha256 = package.Request.ArchiveSha256,
                completedUtc = DateTimeOffset.UtcNow.ToString("O"),
                importedPresets = build.PresetCount,
                importedResources = build.ResourceCount,
                importedSettings,
                reviewRequired = build.ReviewRequiredCount,
                issueCount = issues.Count,
                issues,
                previousImportBackup = backupDirectory,
                sourceDiagnostics = package.Data.Diagnostics
            }, Formatting.Indented));
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(MigrationImportService),
                $"Cannot write the migration report: {exception.Message}");
        }
    }

    internal static void WriteCompletionMarker(
        VerifiedMigrationPackage package,
        MigrationImportBuildResult build,
        int importedSettings,
        IReadOnlyCollection<MigrationImportIssue> issues)
    {
        if (issues.Count > 0)
            return;
        string directory = Path.Combine(
            Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "Completed");
        Directory.CreateDirectory(directory);
        string marker = Path.Combine(directory, package.Request.MigrationId.ToString("N") + ".json");
        string temporary = marker + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonConvert.SerializeObject(new
        {
            schemaVersion = 1,
            migrationId = package.Request.MigrationId.ToString("D"),
            archiveSha256 = package.Request.ArchiveSha256,
            completedUtc = DateTimeOffset.UtcNow.ToString("O"),
            importedPresets = build.PresetCount,
            importedResources = build.ResourceCount,
            importedSettings,
            issueCount = issues.Count,
            issues
        }));
        File.Move(temporary, marker, overwrite: true);
    }
}
