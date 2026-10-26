using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static CDPIUI.Helper.Migration.MigrationArchiveGuard;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// Checks the raw JSON shape of the archive metadata before it is deserialized:
/// every object must have its required properties and nothing unknown or duplicated.
/// </summary>
internal static class MigrationArchiveJsonSchemaValidator
{
    public static void ValidateManifestJson(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes);
        JsonElement root = document.RootElement;
        ValidateObject(root, "manifest",
            ["SchemaVersion", "PackageKind", "MigrationId", "CreatedUtc", "SourceProduct",
             "TargetProduct", "SourceRoot", "PresetCollectionNameRu", "PresetCollectionNameEn",
             "Selection", "MigrationDataPath", "MigrationDataSchemaVersion", "MigrationDataSize",
             "MigrationDataSha256", "Items", "Diagnostics"],
            ["SchemaVersion", "PackageKind", "MigrationId", "CreatedUtc", "SourceProduct",
             "TargetProduct", "SourceRoot", "PresetCollectionNameRu", "PresetCollectionNameEn",
             "Selection", "MigrationDataPath", "MigrationDataSchemaVersion", "MigrationDataSize",
             "MigrationDataSha256", "Items", "Diagnostics"]);
        ValidateObject(root.GetProperty("Selection"), "manifest selection",
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"],
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"]);
        foreach (JsonElement item in RequireArray(root, "Items"))
            ValidateObject(item, "manifest item",
                ["Kind", "Component", "OriginalRelativePath", "ArchivePath", "Size", "Sha256",
                 "IsPresetDependency", "ReferencedBy"],
                ["Kind", "Component", "OriginalRelativePath", "ArchivePath", "Size", "Sha256",
                 "IsPresetDependency", "ReferencedBy"]);
        foreach (JsonElement diagnostic in RequireArray(root, "Diagnostics"))
            ValidateDiagnosticObject(diagnostic);
    }

    public static void ValidateMigrationDataJson(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes);
        JsonElement root = document.RootElement;
        ValidateObject(root, "migration data",
            ["SchemaId", "SchemaVersion", "MigrationId", "PackageKind", "CreatedUtc", "Source",
             "PresetCollection", "Selection", "Presets", "Settings", "Resources", "Diagnostics"],
            ["SchemaId", "SchemaVersion", "MigrationId", "PackageKind", "CreatedUtc", "Source",
             "PresetCollection", "Selection", "Presets", "Settings", "Resources", "Diagnostics"]);
        ValidateObject(root.GetProperty("Source"), "source", ["Product", "FolderName"], ["Product", "FolderName"]);
        ValidateObject(root.GetProperty("PresetCollection"), "preset collection",
            ["Id", "NameRu", "NameEn"], ["Id", "NameRu", "NameEn"]);
        ValidateObject(root.GetProperty("Selection"), "selection",
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"],
            ["IncludePresets", "IncludeApplicationSettings", "IncludeSiteLists"]);

        foreach (JsonElement preset in RequireArray(root, "Presets"))
        {
            ValidateObject(preset, "preset",
                ["Id", "CollectionId", "Component", "Name", "SourceRelativePath", "PayloadPath",
                 "SourceSha256", "SourceFormat", "ParameterMode", "IsUserPreset", "RequiresReview", "Resources"],
                ["Id", "CollectionId", "Component", "Name", "SourceRelativePath", "PayloadPath",
                 "SourceSha256", "SourceFormat", "ParameterMode", "CustomParameters", "IsUserPreset",
                 "RequiresReview", "Resources"]);
            foreach (JsonElement link in RequireArray(preset, "Resources"))
                ValidateObject(link, "preset resource link",
                    ["OriginalReference", "Kind", "IsResolved"],
                    ["OriginalReference", "Kind", "IsResolved", "ResourceId", "ResolvedRelativePath", "DiagnosticCode"]);
        }
        foreach (JsonElement setting in RequireArray(root, "Settings"))
            ValidateObject(setting, "setting",
                ["Id", "SourceRelativePath", "PayloadPath", "SourceFormat", "Ordinal", "Key", "ValueType", "ImportPolicy"],
                ["Id", "SourceRelativePath", "PayloadPath", "SourceFormat", "Ordinal", "Section", "Key", "JsonPath",
                 "Value", "ValueType", "SemanticKey", "ImportPolicy"]);
        foreach (JsonElement resource in RequireArray(root, "Resources"))
            ValidateObject(resource, "resource",
                ["Id", "Kind", "Component", "SourceRelativePath", "PayloadPath", "Sha256", "Size",
                 "IsPresetDependency", "ReferencedByPresetIds"],
                ["Id", "Kind", "Component", "SourceRelativePath", "PayloadPath", "Sha256", "Size",
                 "IsPresetDependency", "ReferencedByPresetIds"]);
        foreach (JsonElement diagnostic in RequireArray(root, "Diagnostics"))
            ValidateDiagnosticObject(diagnostic);
    }

    private static JsonDocument ParseJson(byte[] bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
        }
        catch (JsonException exception)
        {
            throw Invalid($"The migration metadata is invalid JSON: {exception.Message}");
        }
    }

    private static void ValidateDiagnosticObject(JsonElement value) =>
        ValidateObject(value, "diagnostic", ["Severity", "Code", "Message"],
            ["Severity", "Code", "Message", "RelatedPath"]);

    private static JsonElement.ArrayEnumerator RequireArray(JsonElement owner, string propertyName)
    {
        JsonElement value = owner.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Array)
            throw Invalid($"'{propertyName}' must be an array.");
        return value.EnumerateArray();
    }

    private static void ValidateObject(
        JsonElement value,
        string context,
        IReadOnlyCollection<string> required,
        IReadOnlyCollection<string> allowed)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Invalid($"The {context} must be an object.");
        HashSet<string> allowedSet = new(allowed, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!allowedSet.Contains(property.Name) || !seen.Add(property.Name))
                throw Invalid($"The {context} contains an unexpected or duplicate property '{property.Name}'.");
        }
        if (required.Any(property => !seen.Contains(property)))
            throw Invalid($"The {context} is missing a required property.");
    }
}
