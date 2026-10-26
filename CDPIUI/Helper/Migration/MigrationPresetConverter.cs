using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// Converts a GoodbyeDPI-UI preset from the migration archive to a CDPI UI configuration.
/// </summary>
internal static class MigrationPresetConverter
{
    private const long MaximumPresetSize = 8L * 1024L * 1024L;

    /// <summary>
    /// Reads the preset, points its resource references at the imported files and fills
    /// the fields a CDPI UI configuration needs.
    /// </summary>
    internal static JObject Convert(
        ZipArchiveEntry entry,
        MigrationPreset preset,
        IReadOnlyDictionary<string, MigrationStoredResource> storedResources)
    {
        JObject config = CreateConfig(entry, preset);
        RewriteResourceReferences(config, preset, storedResources);
        MigrationComponentRequirement component = GoodbyeDpiComponentMapper.Map(preset);
        config["meta"] = "pUC:v1.0";
        config["target"] = new JArray(component.ConfigTargetId, "%CURRENT%");
        config["name"] = preset.Name;
        config["jparams"] ??= new JObject();
        config["variables"] ??= new JArray();
        config["startup_string"] ??= string.Empty;
        RemoveRuntimeOnlyProperties(config);
        return config;
    }

    // Strings that look like dates stay strings, and numbers keep their exact decimal value.
    internal static JObject ParseObject(string json)
    {
        using JsonTextReader reader = new(new StringReader(json))
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal
        };
        JObject result = JObject.Load(reader);
        while (reader.Read())
        {
            if (reader.TokenType != JsonToken.Comment)
                throw new JsonReaderException("Additional text found after the JSON object.");
        }
        return result;
    }

    private static JObject CreateConfig(ZipArchiveEntry entry, MigrationPreset preset)
    {
        if (entry.Length < 0 || entry.Length > MaximumPresetSize)
            throw new InvalidDataException($"Preset '{preset.Name}' exceeds the import size limit.");
        string json;
        using (StreamReader reader = new(entry.Open(), new UTF8Encoding(false, true), true))
            json = reader.ReadToEnd();

        if (preset.ParameterMode == "custom-parameters")
        {
            return new JObject
            {
                ["startup_string"] = preset.CustomParameters ?? string.Empty
            };
        }

        try
        {
            JObject source = ParseObject(json);
            if (source["custom_parameters"]?.Type == JTokenType.String)
                source["startup_string"] = source["custom_parameters"]!.Value<string>();
            source.Remove("custom_parameters");
            return source;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Preset '{preset.Name}' is not valid JSON.", exception);
        }
    }

    private static void RewriteResourceReferences(
        JObject config,
        MigrationPreset preset,
        IReadOnlyDictionary<string, MigrationStoredResource> resources)
    {
        List<(string Source, string Destination)> replacements = [];
        foreach (MigrationPresetResourceLink link in preset.Resources)
        {
            if (string.IsNullOrWhiteSpace(link.OriginalReference))
                continue;
            if (!link.IsResolved)
            {
                replacements.Add((link.OriginalReference, QuotePath(link.OriginalReference)));
                continue;
            }
            if (link.ResourceId == null ||
                !resources.TryGetValue(link.ResourceId, out MigrationStoredResource? resource))
            {
                throw new InvalidDataException(
                    $"Preset '{preset.Name}' depends on a resource that could not be imported.");
            }
            replacements.Add((link.OriginalReference, QuotePath(resource.Reference)));
        }
        RewriteToken(config, replacements);
    }

    private static void RewriteToken(
        JToken token,
        IReadOnlyCollection<(string Source, string Destination)> replacements)
    {
        if (token is JValue { Type: JTokenType.String } value)
        {
            string text = value.Value<string>() ?? string.Empty;
            foreach ((string source, string destination) in replacements)
            {
                string alternate = source.Contains('\\') ? source.Replace('\\', '/') : source.Replace('/', '\\');
                foreach (string candidate in new[] { source, alternate }.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    text = ReplaceOrdinalIgnoreCase(text, $"\"{candidate}\"", destination);
                    text = ReplaceOrdinalIgnoreCase(text, $"'{candidate}'", destination);
                    text = ReplacePathIgnoreCase(text, candidate, destination);
                }
            }
            value.Value = text;
            return;
        }

        foreach (JToken child in token.Children().ToList())
            RewriteToken(child, replacements);
    }

    private static string ReplaceOrdinalIgnoreCase(string input, string source, string destination)
    {
        if (string.IsNullOrEmpty(source))
            return input;
        return Regex.Replace(
            input,
            Regex.Escape(source),
            _ => destination,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Replaces <paramref name="source"/> only where it is a whole path, so that rewriting
    /// "list.txt" leaves "blacklist.txt" and "lists\list.txt.bak" alone.
    /// </summary>
    private static string ReplacePathIgnoreCase(string input, string source, string destination)
    {
        if (string.IsNullOrEmpty(source))
            return input;
        return Regex.Replace(
            input,
            $@"(?<![\w.\\/:-]){Regex.Escape(source)}(?![\w.\\/-])",
            _ => destination,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    private static string QuotePath(string path)
    {
        return "\"" + path.Trim().Trim('"') + "\"";
    }

    private static void RemoveRuntimeOnlyProperties(JObject config)
    {
        foreach (string property in new[]
        {
            "file_name", "packId", "not_converted_name", "toggle_lists",
            "IsLegacy", "MarkAsRemoved", "custom_parameters"
        })
        {
            config.Remove(property);
        }
    }
}
