using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Shared.Migration;
using static CDPIUI.Helper.Migration.MigrationArchiveGuard;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// Checks the deserialized migration-data document against the signed manifest and the
/// activation request: identity, resources, presets, settings and the user's selection.
/// </summary>
internal static class MigrationDataValidator
{
    private const string MigratedCollectionId = "migrated-from-goodbyedpi-ui";

    public static void Validate(
        MigrationDataDocument data,
        MigrationArchiveManifest manifest,
        GoodbyeDpiMigrationActivationRequest request)
    {
        ValidateIdentity(data, manifest, request);

        Dictionary<string, MigrationArchiveItem> manifestItems = manifest.Items.ToDictionary(
            item => item.ArchivePath!, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, MigrationResource> resources = ValidateResources(data.Resources, manifestItems);
        HashSet<string> presetIds = ValidatePresets(data.Presets, manifestItems, resources);
        ValidateResourceReferences(data.Resources, presetIds);
        ValidateSettings(data.Settings, manifestItems);

        if (!manifest.Selection.IncludePresets && data.Presets.Count != 0 ||
            !manifest.Selection.IncludeApplicationSettings && data.Settings.Count != 0)
            throw Invalid("The migration data exceeds the user's selection.");
        ValidateDiagnostics(data.Diagnostics);
    }

    public static MigrationArchiveItem RequireManifestItem(
        IReadOnlyDictionary<string, MigrationArchiveItem> manifestItems,
        string? payloadPath,
        string? sourcePath)
    {
        EnsureSafePath(payloadPath);
        EnsureSafePath(sourcePath);
        if (!manifestItems.TryGetValue(payloadPath!, out MigrationArchiveItem? item) ||
            !string.Equals(item.OriginalRelativePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            throw Invalid("A migration-data payload is absent from the manifest.");
        return item;
    }

    private static void ValidateIdentity(
        MigrationDataDocument data,
        MigrationArchiveManifest manifest,
        GoodbyeDpiMigrationActivationRequest request)
    {
        const string message = "The migration-data identity does not match the manifest.";
        if (data.SchemaId != "gdpiui-migration-data" || data.SchemaVersion != 1 ||
            data.PackageKind != "migration" || data.Source?.Product != "GoodbyeDPI-UI" ||
            data.PresetCollection?.Id != MigratedCollectionId ||
            data.PresetCollection.NameRu != "Перенесенные из GoodbyeDPI-UI" ||
            data.PresetCollection.NameEn != "Migrated from GoodbyeDPI-UI")
            throw Invalid(message);
        if (!Guid.TryParse(data.MigrationId, out Guid migrationId) || migrationId != request.MigrationId ||
            data.MigrationId != manifest.MigrationId || data.CreatedUtc != manifest.CreatedUtc)
            throw Invalid(message);
        if (data.Selection == null || manifest.Selection == null ||
            data.Selection.IncludePresets != manifest.Selection.IncludePresets ||
            data.Selection.IncludeApplicationSettings != manifest.Selection.IncludeApplicationSettings ||
            data.Selection.IncludeSiteLists != manifest.Selection.IncludeSiteLists)
            throw Invalid(message);
    }

    private static Dictionary<string, MigrationResource> ValidateResources(
        IEnumerable<MigrationResource> dataResources,
        IReadOnlyDictionary<string, MigrationArchiveItem> manifestItems)
    {
        HashSet<string> resourceIds = new(StringComparer.Ordinal);
        Dictionary<string, MigrationResource> resources = new(StringComparer.Ordinal);

        foreach (MigrationResource resource in dataResources)
        {
            EnsureEntityId(resource.Id, "resource");
            if (!resourceIds.Add(resource.Id!) || !resources.TryAdd(resource.Id, resource) ||
                resource.Kind is not ("site-list" or "binary") || resource.Size < 0)
                throw Invalid("A resource identity, kind, or size is invalid.");
            MigrationArchiveItem item = RequireManifestItem(manifestItems, resource.PayloadPath, resource.SourceRelativePath);
            string expectedKind = resource.Kind == "binary" ? "BinaryResource" : "SiteList";
            if (item.Kind != expectedKind || item.Size != resource.Size || item.Sha256 != resource.Sha256)
                throw Invalid("A resource does not match its manifest item.");
            EnsureHash(resource.Sha256, resource.Id);
            EnsureText(resource.Component, "resource component", 128);
        }
        return resources;
    }

    private static HashSet<string> ValidatePresets(
        IEnumerable<MigrationPreset> presets,
        IReadOnlyDictionary<string, MigrationArchiveItem> manifestItems,
        IReadOnlyDictionary<string, MigrationResource> resources)
    {
        HashSet<string> presetIds = new(StringComparer.Ordinal);
        foreach (MigrationPreset preset in presets)
        {
            EnsureEntityId(preset.Id, "preset");
            if (!presetIds.Add(preset.Id!) || preset.CollectionId != MigratedCollectionId ||
                preset.SourceFormat != "goodbyedpi-ui-json" ||
                preset.ParameterMode is not ("custom-parameters" or "structured-json") ||
                (preset.ParameterMode == "custom-parameters" && preset.CustomParameters == null))
                throw Invalid("A preset identity or format is invalid.");
            EnsureText(preset.Component, "preset component", 128);
            EnsureText(preset.Name, "preset name", 512);
            MigrationArchiveItem item = RequireManifestItem(manifestItems, preset.PayloadPath, preset.SourceRelativePath);
            if (item.Kind != "Preset" || item.Sha256 != preset.SourceSha256)
                throw Invalid("A preset does not match its manifest item.");
            EnsureHash(preset.SourceSha256, preset.Id);
            ValidatePresetResourceLinks(preset.Resources, resources);
        }
        return presetIds;
    }

    private static void ValidatePresetResourceLinks(
        IEnumerable<MigrationPresetResourceLink> links,
        IReadOnlyDictionary<string, MigrationResource> resources)
    {
        foreach (MigrationPresetResourceLink link in links)
        {
            EnsureText(link.OriginalReference, "resource reference", 4096);
            if (link.Kind is not ("site-list" or "binary"))
                throw Invalid("A preset resource link has an invalid kind.");
            if (link.IsResolved && (link.ResourceId == null || !resources.ContainsKey(link.ResourceId)))
                throw Invalid("A resolved preset resource link points to an unknown resource.");
        }
    }

    private static void ValidateResourceReferences(
        IEnumerable<MigrationResource> resources,
        IReadOnlySet<string> presetIds)
    {
        foreach (MigrationResource resource in resources)
        {
            if (resource.ReferencedByPresetIds.Distinct(StringComparer.Ordinal).Count() !=
                    resource.ReferencedByPresetIds.Length ||
                resource.ReferencedByPresetIds.Any(id => !presetIds.Contains(id)))
                throw Invalid("A resource references an unknown or duplicate preset.");
        }
    }

    private static void ValidateSettings(
        IEnumerable<MigrationSetting> settings,
        IReadOnlyDictionary<string, MigrationArchiveItem> manifestItems)
    {
        HashSet<string> settingIds = new(StringComparer.Ordinal);
        foreach (MigrationSetting setting in settings)
        {
            EnsureEntityId(setting.Id, "setting");
            if (!settingIds.Add(setting.Id!) || setting.SourceFormat is not ("ini" or "json") ||
                setting.Ordinal < 0 || setting.ValueType is not ("string" or "boolean" or "number" or "null") ||
                setting.ImportPolicy is not ("preserve" or "candidate"))
                throw Invalid("A setting identity or format is invalid.");
            EnsureText(setting.Key, "setting key", 512);
            MigrationArchiveItem item = RequireManifestItem(manifestItems, setting.PayloadPath, setting.SourceRelativePath);
            if (item.Kind != "ApplicationSetting")
                throw Invalid("A setting does not match an application-setting manifest item.");
        }
    }
}
