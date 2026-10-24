using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Finds the components a source file can be imported for, from its target, the executables it
/// launches or the options it uses.
/// </summary>
internal static partial class ConfigImportTargetMatcher
{
    public static IReadOnlyList<ConfigImportTarget> FindMatchingTargets(
        string filePath,
        IEnumerable<ConfigImportTarget> targets)
    {
        if (!File.Exists(filePath))
            return [];

        List<ConfigImportTarget> availableTargets = targets
            .GroupBy(target => target.ComponentId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        string extension = Path.GetExtension(filePath);
        if (ConfigShare.ConfigShareService.IsSupported(filePath))
        {
            try
            {
                using var package = new ConfigShare.ConfigShareService().Read(filePath);
                return availableTargets.Where(target => target.ComponentId == package.Config.target![0]).ToList();
            }
            catch { return []; } // Import reports and logs the detailed archive error.
        }
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string? componentId = JSONConvertor.LoadJson<ConfigItem>(filePath)?.target?.FirstOrDefault();
                return availableTargets
                    .Where(target => string.Equals(target.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        if (!extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return [];

        return FindCommandFileTargets(filePath, extension, availableTargets);
    }

    private static IReadOnlyList<ConfigImportTarget> FindCommandFileTargets(
        string filePath,
        string extension,
        List<ConfigImportTarget> availableTargets)
    {
        string text;
        try
        {
            // The same encoding detection as the Zapret importer: a .bat in cp866 or cp1251 lost its Cyrillic text.
            text = CDPIUI.Core.System.BatchFileEncoding.ReadAllText(filePath);
        }
        catch
        {
            return [];
        }

        List<ConfigImportTarget> executableMatches = availableTargets
            .Where(target => ContainsExecutableReference(text, target.Executable))
            .ToList();
        if (executableMatches.Count > 0)
            return executableMatches;

        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return [];

        string? inferredExecutable = BuiltinVersionRegex().IsMatch(text) ||
                                     text.Contains("--lua-desync", StringComparison.OrdinalIgnoreCase)
            ? "winws2"
            : text.Contains("--dpi-desync", StringComparison.OrdinalIgnoreCase)
                ? "winws"
                : null;
        if (inferredExecutable == null)
            return [];

        return availableTargets
            .Where(target => ConfigImportExecutables.Normalize(target.Executable)
                .Equals(inferredExecutable, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool ContainsExecutableReference(string text, string executable)
    {
        string name = Regex.Escape(ConfigImportExecutables.Normalize(executable));
        return Regex.IsMatch(
            text,
            $@"(?i)(?:%~dp0|(?<![A-Za-z0-9_.-])){name}(?:\.exe)?(?![A-Za-z0-9_.-])");
    }

    [GeneratedRegex(@"^\s*#\s*BuiltinVersion\s*:\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex BuiltinVersionRegex();
}
