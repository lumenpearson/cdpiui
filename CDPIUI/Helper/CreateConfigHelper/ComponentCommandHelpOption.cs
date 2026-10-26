using System;
using System.Collections.Generic;
using System.Linq;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ComponentCommandHelpOption
{
    public string DisplayName => Names.FirstOrDefault(name => name.StartsWith("--", StringComparison.Ordinal))
        ?? Names.FirstOrDefault()
        ?? Syntax;
    public IReadOnlyList<string> Names { get; init; } = [];
    public string GroupName { get; init; } = string.Empty;
    public string Syntax { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ArgumentPlaceholder { get; init; } = string.Empty;
    public bool IsArgumentRequired { get; init; }

    public bool Matches(string flag)
    {
        string normalizedFlag = NormalizeOptionName(flag);
        return Names.Any(name => string.Equals(
            NormalizeOptionName(name),
            normalizedFlag,
            StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeOptionName(string value)
    {
        string normalized = (value ?? string.Empty).Trim();
        int equalsIndex = normalized.IndexOf('=');
        return equalsIndex >= 0 ? normalized[..equalsIndex] : normalized;
    }
}
