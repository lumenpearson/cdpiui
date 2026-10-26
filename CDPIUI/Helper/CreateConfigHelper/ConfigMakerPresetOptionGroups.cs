using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Helper.CreateConfigHelper;

// Splits a parsed preset command into the common part and the "--new" separated profiles.
internal static class ConfigMakerPresetOptionGroups
{
    public static int FindFirstProfileOptionIndex(IReadOnlyList<ConfigCommandOption> options) => options
        .Select((option, index) => new { option, index })
        .Where(item => IsProfileOption(item.option.Name))
        .Select(item => item.index)
        .DefaultIfEmpty(options.Count)
        .First();

    public static List<List<ConfigCommandOption>> SplitProfiles(IEnumerable<ConfigCommandOption> options)
    {
        List<List<ConfigCommandOption>> profiles = [];
        List<ConfigCommandOption> current = [];
        foreach (ConfigCommandOption option in options)
        {
            if (string.Equals(option.Name, "--new", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Count > 0)
                {
                    profiles.Add(current);
                    current = [];
                }
                continue;
            }
            current.Add(option);
        }
        if (current.Count > 0)
        {
            profiles.Add(current);
        }
        return profiles;
    }

    public static string FindProfileName(IEnumerable<ConfigCommandOption> options) => options
        .FirstOrDefault(option => string.Equals(option.Name, "--name", StringComparison.OrdinalIgnoreCase))
        ?.Value;

    public static bool IsCommonFilter(string optionName) =>
        optionName.StartsWith("--wf-", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--ssid-filter", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--nlm-", StringComparison.OrdinalIgnoreCase);

    public static bool IsProfileOption(string optionName) =>
        string.Equals(optionName, "--new", StringComparison.OrdinalIgnoreCase) ||
        IsProfileFilter(optionName) ||
        optionName.StartsWith("--payload", StringComparison.OrdinalIgnoreCase) ||
        optionName.EndsWith("-range", StringComparison.OrdinalIgnoreCase) ||
        optionName.Contains("desync", StringComparison.OrdinalIgnoreCase);

    public static bool IsProfileFilter(string optionName) =>
        optionName.StartsWith("--filter-", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--hostlist", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--ipset", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(optionName, "--name", StringComparison.OrdinalIgnoreCase);
}
