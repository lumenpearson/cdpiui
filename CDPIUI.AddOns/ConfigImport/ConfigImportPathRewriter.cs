using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Rewrites the file paths of an imported Config to the places the import saves the files to.
/// </summary>
internal static class ConfigImportPathRewriter
{
    public static void RewriteResourcePaths(
        ConfigItem config,
        IReadOnlyList<ConfigImportResource> resources)
    {
        config.startup_string = RewriteResources(config.startup_string, resources);
        if (config.variables != null)
        {
            for (int index = 0; index < config.variables.Count; index++)
                config.variables[index] = RewriteResources(config.variables[index], resources)!;
        }
        if (config.commaVars != null)
        {
            foreach (string key in config.commaVars.Keys.ToList())
                config.commaVars[key] = RewriteResources(config.commaVars[key], resources)!;
        }
        if (config.availableCommaVarsValues != null)
        {
            foreach (AvailableVarValues values in config.availableCommaVarsValues)
            {
                if (values.Values == null)
                    continue;
                for (int index = 0; index < values.Values.Count; index++)
                    values.Values[index] = RewriteResources(values.Values[index], resources)!;
            }
        }
    }

    private static string? RewriteResources(
        string? value,
        IReadOnlyList<ConfigImportResource> resources)
    {
        if (value == null)
            return null;

        string result = value;
        foreach (ResourceDirectoryMapping mapping in BuildDirectoryMappings(resources))
            result = RewriteResourceDirectory(result, mapping);

        foreach (ConfigImportResource resource in resources)
        {
            string normalizedRelativePath = resource.SourceRelativePath.Replace('\\', '/').TrimStart('/');
            string replacement = $"$GETCURRENTDIR()/{resource.SavedRelativePath}";
            result = ReplacePath(result, resource.ExpectedPath, replacement);

            string pathPattern = CreatePathPattern(normalizedRelativePath);
            result = Regex.Replace(
                result,
                $@"\$GETCURRENTDIR\(\)[\\/]+{pathPattern}",
                _ => replacement,
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                $@"(?<![A-Za-z0-9_./\\$])(?<at>@?)(?:\.[\\/])?{pathPattern}(?![A-Za-z0-9_./\\])",
                match => $"{match.Groups["at"].Value}{replacement}",
                RegexOptions.IgnoreCase);
        }
        return result;
    }

    private static IReadOnlyList<ResourceDirectoryMapping> BuildDirectoryMappings(
        IReadOnlyList<ConfigImportResource> resources)
    {
        return resources
            .Select(resource => new
            {
                SourceDirectory = NormalizeRelativeDirectory(resource.SourceRelativePath),
                AbsoluteDirectory = Path.GetDirectoryName(resource.ExpectedPath),
                SavedDirectory = NormalizeRelativeDirectory(resource.SavedRelativePath),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceDirectory) &&
                           !string.IsNullOrWhiteSpace(item.SavedDirectory))
            .GroupBy(item => item.SourceDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                string[] savedDirectories = group
                    .Select(item => item.SavedDirectory)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (savedDirectories.Length != 1)
                    return null;

                return new ResourceDirectoryMapping(
                    group.Key,
                    group.Select(item => item.AbsoluteDirectory)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(path => Path.GetFullPath(path!))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    savedDirectories[0]);
            })
            .Where(mapping => mapping != null)
            .Cast<ResourceDirectoryMapping>()
            .ToArray();
    }

    private static string RewriteResourceDirectory(
        string value,
        ResourceDirectoryMapping mapping)
    {
        string sourcePattern = CreatePathPattern(mapping.SourceDirectory);
        string replacement = $"$GETCURRENTDIR()/{mapping.SavedDirectory}";
        string result = Regex.Replace(
            value,
            $@"\$GETCURRENTDIR\(\)[\\/]+{sourcePattern}(?=[\\/]|$)",
            _ => replacement,
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            $@"%~dp0{sourcePattern}(?=[\\/]|$)",
            _ => replacement,
            RegexOptions.IgnoreCase);

        foreach (string absoluteDirectory in mapping.AbsoluteDirectories)
        {
            result = Regex.Replace(
                result,
                $@"{CreatePathPattern(absoluteDirectory)}(?=[\\/]|$)",
                _ => replacement,
                RegexOptions.IgnoreCase);
        }

        return Regex.Replace(
            result,
            $@"(?<![A-Za-z0-9_./\\$])(?:\.[\\/])?{sourcePattern}(?=[\\/])",
            _ => replacement,
            RegexOptions.IgnoreCase);
    }

    private static string NormalizeRelativeDirectory(string path)
    {
        string normalized = path.Replace('\\', '/').Trim('/');
        int separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex < 0 ? string.Empty : normalized[..separatorIndex];
    }

    private static string ReplacePath(string value, string path, string replacement)
    {
        if (string.IsNullOrWhiteSpace(path))
            return value;
        return Regex.Replace(
            value,
            CreatePathPattern(path),
            _ => replacement,
            RegexOptions.IgnoreCase);
    }

    private static string CreatePathPattern(string path) =>
        Regex.Escape(path.Replace('\\', '/')).Replace("/", @"[\\/]+");

    private sealed record ResourceDirectoryMapping(
        string SourceDirectory,
        IReadOnlyList<string> AbsoluteDirectories,
        string SavedDirectory);
}
