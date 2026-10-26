using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Reports the %VARIABLE% and preset:// references of a preset command that do not resolve.
internal static class ConfigMakerPresetReferenceValidator
{
    public static void AddDiagnostics(
        string commandText,
        IEnumerable<ConfigMakerVariableDefinition> variables,
        Func<string, bool> isKnownResource,
        ICollection<ComponentCommandDiagnostic> diagnostics)
    {
        string normalized = (commandText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            foreach (Match match in Regex.Matches(
                         lines[lineIndex],
                         "%[A-Za-z][A-Za-z0-9_]*%",
                         RegexOptions.CultureInvariant))
            {
                string name = match.Value.Trim('%');
                bool known = variables.Any(variable =>
                    string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase));
                if (known ||
                    string.Equals(name, "CURRENT", StringComparison.OrdinalIgnoreCase) ||
                    Environment.GetEnvironmentVariable(name) != null)
                {
                    continue;
                }
                diagnostics.Add(new ComponentCommandDiagnostic
                {
                    Code = "CFG004",
                    Kind = ComponentCommandDiagnosticKind.UnresolvedVariable,
                    Severity = ComponentCommandDiagnosticSeverity.Error,
                    Token = match.Value,
                    Line = lineIndex + 1,
                    Column = match.Index + 1,
                });
            }
            foreach (Match match in Regex.Matches(
                         lines[lineIndex],
                         "preset://[A-Za-z0-9_.-]+",
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (isKnownResource(match.Value))
                {
                    continue;
                }
                diagnostics.Add(new ComponentCommandDiagnostic
                {
                    Code = "CFG005",
                    Kind = ComponentCommandDiagnosticKind.UnresolvedResource,
                    Severity = ComponentCommandDiagnosticSeverity.Error,
                    Token = match.Value,
                    Line = lineIndex + 1,
                    Column = match.Index + 1,
                });
            }
        }
    }
}
