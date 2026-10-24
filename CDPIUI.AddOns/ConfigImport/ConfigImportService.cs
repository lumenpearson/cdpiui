using CDPIUI.AddOns.ConfigImport.Generic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Static importer for command files and text configs. The selected component
/// supplies the executable name; source scripts are never executed.
/// </summary>
public sealed class ConfigImportService
{
    public const string CurrentVersionPlaceholder = "%CURRENT%";

    public ConfigImportResult Import(string filePath, ConfigImportTarget? target = null, string requestedTarget = "")
    {
        if (ConfigShare.ConfigShareService.IsSupported(filePath))
            return ConfigSharePackageImporter.Import(filePath, target);

        target ??= GetConfigImportTarget(filePath, requestedTarget);

        string fullPath = TryGetFullPath(filePath) ?? filePath;
        if (!ValidateRequest(fullPath, target, out ConfigImportResult? invalidResult))
            return invalidResult!;

        string executable = ConfigImportExecutables.Normalize(target.Executable);
        if (ConfigImportExecutables.IsZapret(executable))
            return ZapretTargetImporter.Import(fullPath, target, executable);

        return GenericCommandImporter.Import(fullPath, target, executable);
    }

    public ConfigImportResult Retarget(ConfigImportResult result, ConfigImportTarget target)
    {
        var issues = result.Issues.ToList();
        ConfigItem? config = result.Config == null
            ? null
            : JSONConvertor.DeserializeObject<ConfigItem>(JSONConvertor.SerializeObject(result.Config));
        if (config != null)
        {
            if (ConfigImportExecutables.Normalize(result.Target.Executable).Equals("winws", StringComparison.OrdinalIgnoreCase) &&
                ConfigImportExecutables.Normalize(target.Executable).Equals("winws2", StringComparison.OrdinalIgnoreCase))
            {
                config = ConvertToZapret2(config, result, issues);
            }

            if (config == null)
                return CreateRetargetedResult(result, target, null, issues);

            string version = ConfigImportExecutables.AreEqual(result.Target.Executable, target.Executable) &&
                             config.target is { Count: > 1 } &&
                             !string.IsNullOrWhiteSpace(config.target[1])
                ? config.target[1]
                : ConfigImportTargets.ResolveVersion(target);
            config.target = [target.ComponentId, version];
            config.packId = null;
        }

        return CreateRetargetedResult(result, target, config, issues);
    }

    private static ConfigItem? ConvertToZapret2(
        ConfigItem config,
        ConfigImportResult result,
        List<ConfigImportIssue> issues)
    {
        config.packId = Path.GetDirectoryName(result.SourcePath);
        string expandedLegacyStartup = ConfigurationService.GetStartupParametersByConfigItem(config);
        ZapretConversionResult conversion = new Zapret1ToZapret2Converter().Convert(expandedLegacyStartup);
        issues.AddRange(conversion.Issues.Select(issue => new ConfigImportIssue(
            issue.Severity == ZapretConversionIssueSeverity.Error
                ? ConfigImportIssueSeverity.Error
                : ConfigImportIssueSeverity.Warning,
            issue.Code,
            issue.Message,
            result.SourcePath)));

        if (!conversion.IsSuccessful)
            return null;

        config.startup_string = conversion.StartupString;
        config.jparams = [];
        config.variables = [];
        config.commaVars = null;
        config.availableCommaVarsValues = null;
        return config;
    }

    public ConfigImportResult ApplyMissingFileResolutions(
        ConfigImportResult result,
        IEnumerable<ConfigImportMissingFileResolution> resolutions)
    {
        var missingFiles = result.MissingReferencedFiles
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, ConfigImportMissingFileResolution>(StringComparer.OrdinalIgnoreCase);
        var issues = result.Issues.ToList();

        foreach (ConfigImportMissingFileResolution resolution in resolutions)
        {
            string missingPath = Path.GetFullPath(resolution.MissingPath);
            if (!missingFiles.Contains(missingPath))
                continue;

            string? replacementPath = string.IsNullOrWhiteSpace(resolution.ReplacementPath)
                ? null
                : Path.GetFullPath(resolution.ReplacementPath);
            if (replacementPath != null && !File.Exists(replacementPath))
            {
                issues.Add(new(
                    ConfigImportIssueSeverity.Error,
                    "MISSING_FILE_REPLACEMENT_NOT_FOUND",
                    $"The selected replacement file was not found: {replacementPath}",
                    replacementPath));
            }

            normalized[missingPath] = new ConfigImportMissingFileResolution(missingPath, replacementPath);
        }

        return new ConfigImportResult
        {
            SharedPackage = result.SharedPackage,
            Config = result.Config,
            Target = result.Target,
            SourcePath = result.SourcePath,
            Issues = issues.AsReadOnly(),
            SourceFiles = result.SourceFiles,
            ReferencedFiles = result.ReferencedFiles,
            MissingReferencedFiles = result.MissingReferencedFiles,
            MissingFileResolutions = normalized.Values.ToList().AsReadOnly(),
            GeneratedFiles = result.GeneratedFiles,
        };
    }

    private static ConfigImportResult CreateRetargetedResult(
        ConfigImportResult result,
        ConfigImportTarget target,
        ConfigItem? config,
        IReadOnlyList<ConfigImportIssue> issues) =>
        new()
        {
            SharedPackage = result.SharedPackage,
            Config = config,
            Target = target,
            SourcePath = result.SourcePath,
            Issues = issues,
            SourceFiles = result.SourceFiles,
            ReferencedFiles = result.ReferencedFiles,
            MissingReferencedFiles = result.MissingReferencedFiles,
            MissingFileResolutions = result.MissingFileResolutions,
            GeneratedFiles = result.GeneratedFiles,
        };

    public IReadOnlyList<ConfigImportTarget> FindMatchingTargets(
        string filePath,
        IEnumerable<ConfigImportTarget> targets) =>
        ConfigImportTargetMatcher.FindMatchingTargets(filePath, targets);

    internal static bool TryParseSet(string line, out string name, out string value) =>
        GenericBatchSyntax.TryParseSet(line, out name, out value);

    private static bool ValidateRequest(
        string filePath,
        ConfigImportTarget target,
        out ConfigImportResult? errorResult)
    {
        var issues = new List<ConfigImportIssue>();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            issues.Add(new(ConfigImportIssueSeverity.Error, "FILE_NOT_FOUND", "Config source file was not found.", filePath));
        if (string.IsNullOrWhiteSpace(target.ComponentId))
            issues.Add(new(ConfigImportIssueSeverity.Error, "TARGET_REQUIRED", "A target component was not selected.", filePath));
        if (string.IsNullOrWhiteSpace(target.Executable))
            issues.Add(new(ConfigImportIssueSeverity.Error, "EXECUTABLE_REQUIRED", "The selected component has no executable in the database.", filePath));

        if (issues.Count == 0)
        {
            errorResult = null;
            return true;
        }

        errorResult = CreateResult(target, filePath, null, issues);
        return false;
    }

    private static string? TryGetFullPath(string path)
    {
        try
        { return Path.GetFullPath(path); }
        catch { return null; }
    }

    private static ConfigImportResult CreateResult(
        ConfigImportTarget target,
        string sourcePath,
        ConfigItem? config,
        IReadOnlyList<ConfigImportIssue> issues) =>
        new()
        {
            Config = config,
            Target = target,
            SourcePath = sourcePath,
            Issues = issues,
            SourceFiles = File.Exists(sourcePath) ? [sourcePath] : [],
            ReferencedFiles = [],
            MissingReferencedFiles = [],
            MissingFileResolutions = [],
            GeneratedFiles = [],
        };

    private ConfigImportTarget GetConfigImportTarget(string filePath, string requestedTarget)
    {
        var components = DatabaseHelper.Instance.GetItemsByType("component");
        // A component without an executable cannot run an imported config, as in the target list.
        List<ConfigImportTarget> targets = [.. components.Where(item => !string.IsNullOrWhiteSpace(item.Executable)).Select(ConfigImportTargets.Create)];

        var matches = FindMatchingTargets(filePath, targets);
        return matches.FirstOrDefault()
            ?? FindRequestedTarget(targets, requestedTarget)
            ?? targets.FirstOrDefault()
            ?? new ConfigImportTarget(string.Empty, string.Empty, string.Empty, null);
    }

    private static ConfigImportTarget? FindRequestedTarget(IReadOnlyList<ConfigImportTarget> targets, string requestedTarget) =>
        targets.FirstOrDefault(target =>
            string.Equals(target.ComponentId, requestedTarget, StringComparison.OrdinalIgnoreCase));
}
