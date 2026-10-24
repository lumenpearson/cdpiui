using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using CDPIUI.Shared.Migration;
using static CDPIUI.Helper.Migration.MigrationArchiveGuard;

namespace CDPIUI.Helper.Migration;

internal sealed class MigrationArchiveInspectionService
{
    private const string ManifestEntryName = "manifest.json";
    private const string MigrationDataEntryName = "migration-data.json";
    private const long MaximumExpandedSize = 1024L * 1024L * 1024L;
    private const long MaximumMetadataSize = 32L * 1024L * 1024L;
    private const int MaximumEntryCount = 10000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        MaxDepth = 64
    };

    public VerifiedMigrationPackage InspectAndStage(
        GoodbyeDpiMigrationActivationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string stagedArchivePath = MigrationArchiveStager.StageArchive(request, cancellationToken);

        using FileStream stream = new(
            stagedArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.SequentialScan);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        Dictionary<string, ZipArchiveEntry> entries = ValidateEntryEnvelope(archive);
        byte[] manifestBytes = ReadEntry(entries[ManifestEntryName], MaximumMetadataSize);
        MigrationArchiveJsonSchemaValidator.ValidateManifestJson(manifestBytes);
        MigrationArchiveManifest manifest = Deserialize<MigrationArchiveManifest>(manifestBytes);
        ValidateManifest(manifest, request, entries);

        ZipArchiveEntry dataEntry = entries[MigrationDataEntryName];
        if (dataEntry.Length != manifest.MigrationDataSize)
            throw Invalid("The migration-data size differs from the signed manifest.");
        byte[] dataBytes = ReadEntry(dataEntry, MaximumMetadataSize);
        if (!HashEquals(dataBytes, manifest.MigrationDataSha256))
            throw Invalid("The migration-data SHA-256 hash differs from the manifest.");

        MigrationArchiveJsonSchemaValidator.ValidateMigrationDataJson(dataBytes);
        MigrationDataDocument data = Deserialize<MigrationDataDocument>(dataBytes);
        MigrationDataValidator.Validate(data, manifest, request);
        VerifyPayloads(entries, manifest, cancellationToken);

        List<MigrationSetting> componentSettings = data.Settings
            .FindAll(group => group.Section == "COMPONENTS");

        IReadOnlyList<MigrationComponentRequirement> components = data.Presets
            .FindAll(item => componentSettings.Find(setting => setting.Key.Equals(item.Component, StringComparison.OrdinalIgnoreCase))?.Value == "True")
            .Select(GoodbyeDpiComponentMapper.Map)
            .GroupBy(item => item.InstallItemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new VerifiedMigrationPackage
        {
            Request = request,
            StagedArchivePath = stagedArchivePath,
            Manifest = manifest,
            Data = data,
            Components = components
        };
    }

    private static Dictionary<string, ZipArchiveEntry> ValidateEntryEnvelope(ZipArchive archive)
    {
        if (archive.Entries.Count is < 2 or > MaximumEntryCount)
            throw Invalid("The migration archive has an invalid entry count.");

        Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        long expandedSize = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            EnsureSafePath(entry.FullName);
            if (entry.FullName.EndsWith('/') || entry.Length < 0 || !entries.TryAdd(entry.FullName, entry))
                throw Invalid("The migration archive contains a directory or duplicate entry.");
            expandedSize = checked(expandedSize + entry.Length);
            if (expandedSize > MaximumExpandedSize)
                throw Invalid("The expanded migration archive exceeds the size limit.");
        }
        if (!entries.ContainsKey(ManifestEntryName) || !entries.ContainsKey(MigrationDataEntryName))
            throw Invalid("The migration archive metadata is incomplete.");
        return entries;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, long maximumSize)
    {
        if (entry.Length < 0 || entry.Length > maximumSize)
            throw Invalid($"The archive entry '{entry.FullName}' exceeds the size limit.");
        using Stream source = entry.Open();
        using MemoryStream destination = new((int)entry.Length);
        source.CopyTo(destination);
        if (destination.Length != entry.Length)
            throw Invalid($"The archive entry '{entry.FullName}' is truncated.");
        return destination.ToArray();
    }

    private static T Deserialize<T>(byte[] bytes) where T : class =>
        JsonSerializer.Deserialize<T>(bytes, SerializerOptions) ??
        throw Invalid("The migration metadata is empty.");

    private static void ValidateManifest(
        MigrationArchiveManifest manifest,
        GoodbyeDpiMigrationActivationRequest request,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        ValidateManifestIdentity(manifest, request);
        EnsureHash(manifest.MigrationDataSha256, "migration-data");
        EnsureText(manifest.SourceRoot, "source root", 512);

        HashSet<string> expectedEntries = new(StringComparer.OrdinalIgnoreCase)
        {
            ManifestEntryName,
            MigrationDataEntryName
        };
        HashSet<string> sourcePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (MigrationArchiveItem item in manifest.Items)
        {
            if (item.Kind is not ("Preset" or "ApplicationSetting" or "SiteList" or "BinaryResource") ||
                item.Size < 0 || item.Size > MaximumExpandedSize)
                throw Invalid("A manifest item has an invalid kind or size.");
            EnsureText(item.Component, "component", 128);
            EnsureSafePath(item.OriginalRelativePath);
            EnsureSafePath(item.ArchivePath);
            if (!item.ArchivePath!.StartsWith("payload/", StringComparison.Ordinal) ||
                !expectedEntries.Add(item.ArchivePath) || !sourcePaths.Add(item.OriginalRelativePath!))
                throw Invalid("A manifest item path is duplicated or outside payload/.");
            EnsureHash(item.Sha256, item.ArchivePath);
            foreach (string reference in item.ReferencedBy)
                EnsureSafePath(reference);
        }

        if (!expectedEntries.SetEquals(entries.Keys))
            throw Invalid("The migration archive contains files not declared by the manifest.");
        ValidateDiagnostics(manifest.Diagnostics);
    }

    private static void ValidateManifestIdentity(
        MigrationArchiveManifest manifest,
        GoodbyeDpiMigrationActivationRequest request)
    {
        const string message = "The migration manifest identity or version is invalid.";
        if (manifest.SchemaVersion != 1 || manifest.MigrationDataSchemaVersion != 1 ||
            manifest.PackageKind != "migration" || manifest.SourceProduct != "GoodbyeDPI-UI" ||
            manifest.TargetProduct != "CDPIUI" || manifest.MigrationDataPath != MigrationDataEntryName ||
            manifest.PresetCollectionNameRu != "Перенесенные из GoodbyeDPI-UI" ||
            manifest.PresetCollectionNameEn != "Migrated from GoodbyeDPI-UI")
            throw Invalid(message);
        if (!Guid.TryParse(manifest.MigrationId, out Guid migrationId) || migrationId != request.MigrationId ||
            !DateTimeOffset.TryParse(manifest.CreatedUtc, out _) || manifest.Selection == null ||
            manifest.MigrationDataSize < 0 || manifest.MigrationDataSize > MaximumMetadataSize)
            throw Invalid(message);
    }

    private static void VerifyPayloads(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        MigrationArchiveManifest manifest,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];
        foreach (MigrationArchiveItem item in manifest.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipArchiveEntry entry = entries[item.ArchivePath!];
            if (entry.Length != item.Size)
                throw Invalid($"The archive entry '{entry.FullName}' has an invalid size.");
            using Stream stream = entry.Open();
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long readTotal = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                readTotal += read;
                if (readTotal > item.Size)
                    throw Invalid($"The archive entry '{entry.FullName}' exceeds its declared size.");
                hash.AppendData(buffer, 0, read);
            }
            string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (readTotal != item.Size || actual != item.Sha256)
                throw Invalid($"The archive entry '{entry.FullName}' failed its integrity check.");
        }
    }

    private static bool HashEquals(byte[] bytes, string? expected)
    {
        EnsureHash(expected, MigrationDataEntryName);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant() == expected;
    }
}
