using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Imports a zapret config for a selected winws or winws2 component through
/// <see cref="ZapretConfigImportService"/> and maps its result to a <see cref="ConfigImportResult"/>.
/// </summary>
internal static class ZapretTargetImporter
{
    public static ConfigImportResult Import(
        string filePath,
        ConfigImportTarget target,
        string executable)
    {
        ZapretConfigGeneration expectedGeneration = executable.Equals("winws2", StringComparison.OrdinalIgnoreCase)
            ? ZapretConfigGeneration.Winws2
            : ZapretConfigGeneration.Winws1;
        var importer = new ZapretConfigImportService();
        ZapretConfigImportResult result = importer.Import(
            filePath,
            new ZapretConfigImportOptions
            {
                CurrentVersionResolver = generation => generation == expectedGeneration
                    ? ConfigImportTargets.ResolveVersion(target)
                    : null,
                DefaultTextConfigGeneration = expectedGeneration,
            });

        var issues = result.Issues.Select(issue => new ConfigImportIssue(
            issue.Severity == ZapretConfigImportIssueSeverity.Error
                ? ConfigImportIssueSeverity.Error
                : ConfigImportIssueSeverity.Warning,
            issue.Code,
            issue.Message,
            issue.SourcePath,
            issue.LineNumber)).ToList();

        IReadOnlyList<string> missingReferencedFiles = FilterComponentLuaFiles(
            result.MissingReferencedFiles,
            result.Config,
            filePath,
            target,
            issues);

        ConfigItem? config = result.Config;
        if (result.Generation.HasValue && result.Generation.Value != expectedGeneration)
        {
            issues.Add(new(
                ConfigImportIssueSeverity.Error,
                "EXECUTABLE_MISMATCH",
                $"The source starts {ConfigImportExecutables.GetZapretExecutable(result.Generation.Value)}, but the selected component uses {executable}.",
                filePath));
            config = null;
        }

        if (config != null)
        {
            string version = config.target is { Count: > 1 } && !string.IsNullOrWhiteSpace(config.target[1])
                ? config.target[1]
                : ConfigImportTargets.ResolveVersion(target);
            config.target = [target.ComponentId, version];
        }

        return new ConfigImportResult
        {
            Config = config,
            Target = target,
            SourcePath = filePath,
            Issues = issues.AsReadOnly(),
            SourceFiles = result.SourceFiles,
            ReferencedFiles = result.ReferencedFiles,
            MissingReferencedFiles = missingReferencedFiles,
            MissingFileResolutions = [],
            GeneratedFiles = result.GeneratedFiles
                .Select(file => new ConfigImportGeneratedFile(file.RelativePath, file.Content))
                .ToList()
                .AsReadOnly(),
        };
    }

    private static IReadOnlyList<string> FilterComponentLuaFiles(
        IReadOnlyList<string> missingFiles,
        ConfigItem? config,
        string sourcePath,
        ConfigImportTarget target,
        List<ConfigImportIssue> issues)
    {
        if (config == null || string.IsNullOrWhiteSpace(target.Directory))
            return missingFiles;

        string sourceDirectory = Path.GetDirectoryName(sourcePath)!;
        var componentProvided = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string missingFile in missingFiles)
        {
            if (!Path.GetExtension(missingFile).Equals(".lua", StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(sourceDirectory, missingFile);
            }
            catch
            {
                continue;
            }

            if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
                continue;
            if (UsesImportedCurrentDirectory(config, relativePath))
                continue;

            string componentPath = Path.GetFullPath(Path.Combine(target.Directory, relativePath));
            if (File.Exists(componentPath))
                componentProvided.Add(Path.GetFullPath(missingFile));
        }

        if (componentProvided.Count == 0)
            return missingFiles;

        issues.RemoveAll(issue =>
            issue.Code == "REFERENCED_FILE_NOT_FOUND" &&
            componentProvided.Any(path => issue.Message.Contains(path, StringComparison.OrdinalIgnoreCase)));
        return missingFiles
            .Where(path => !componentProvided.Contains(Path.GetFullPath(path)))
            .ToList()
            .AsReadOnly();
    }

    private static bool UsesImportedCurrentDirectory(ConfigItem config, string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        string pattern = $@"\$GETCURRENTDIR\(\)[\\/]+{Regex.Escape(normalized).Replace("/", @"[\\/]+")}";
        var values = new List<string?> { config.startup_string };
        if (config.variables != null)
            values.AddRange(config.variables);
        if (config.commaVars != null)
            values.AddRange(config.commaVars.Values);
        if (config.availableCommaVarsValues != null)
            values.AddRange(config.availableCommaVarsValues.SelectMany(item => item.Values ?? []));
        return values.Any(value => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase));
    }
}
