using System.Text.RegularExpressions;
using CDPIUI.Core.LScript;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>
/// Expands the %NAME% variables of a preset startup string.
/// </summary>
internal static partial class ConfigVariableExpander
{
    internal const int MaxVariablePasses = 16;
    internal const int MaxExpandedLength = 64 * 1024;

    [GeneratedRegex("%(?<name>[A-Za-z0-9_.\\-]+)%", RegexOptions.CultureInvariant)]
    private static partial Regex VariableReferenceRegex();

    public static Dictionary<string, string>? GetReadyToUseVariables(
        string? id,
        List<string>? variables,
        Dictionary<string, bool>? jparams)
    {
        // Same rules as ConfigItem.ExpandVariables in the UI: names ignore case, and a name
        // declared twice takes the last value instead of failing the start.
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        string regexString = @"%(.*?)%=(.*?)$";

        if (variables == null || jparams == null)
        {
            return null;
        }

        foreach (string variable in variables)
        {
            Match match = Regex.Match(variable, regexString);

            if (!match.Success)
                continue;

            // A folder script is left for the expansion of the whole startup string, which
            // quotes a path with spaces; expanded here, the path stayed unquoted (issue #2).
            string value = match.Groups[2].Value;
            string trimmed = value.TrimStart();
            result[match.Groups[1].Value] =
                trimmed.StartsWith("$GETCURRENTDIR", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("$GETSRDIR", StringComparison.OrdinalIgnoreCase)
                    ? value
                    : LScriptCore.ExecuteScript(value, callItemId: id, jparams: jparams);
        }

        return result;
    }

    public static string ReplaceVariables(string input, IDictionary<string, string>? readyToUseVars)
    {
        if (string.IsNullOrEmpty(input) || readyToUseVars == null || readyToUseVars.Count == 0)
        {
            return input;
        }

        var vars = new Dictionary<string, string>(readyToUseVars, StringComparer.OrdinalIgnoreCase);

        string evaluator(Match match) =>
            vars.TryGetValue(match.Groups["name"].Value, out string? value) ? value : match.Value;

        // Variables may refer to other variables, as in the editor preview, which expands
        // them the same way; the length limit stops definitions such as %A%=%A%%A%.
        string result = input;
        for (int pass = 0; pass < MaxVariablePasses; pass++)
        {
            string expanded = VariableReferenceRegex().Replace(result, evaluator);
            if (expanded == result || expanded.Length > MaxExpandedLength)
                break;
            result = expanded;
        }
        return result;
    }

    public static string ReplaceCommaVariables(string startupString, Dictionary<string, string>? commaVars)
    {
        Dictionary<string, string>? vars = commaVars;

        if (vars == null || vars.Count == 0)
            return startupString;

        string result = ReplaceVariables(startupString, vars);
        return result;
    }

    /// <summary>
    /// Expands the boolean variables, the comma variables and the scripts of a startup string.
    /// </summary>
    public static string ExpandStartupString(
        string startupString,
        string? packId,
        List<string>? variables,
        Dictionary<string, bool>? jparams,
        Dictionary<string, string>? commaVars)
    {
        Dictionary<string, string>? readyToUseVars = GetReadyToUseVariables(packId, variables, jparams);

        startupString = ReplaceVariables(startupString, readyToUseVars);
        startupString = ReplaceCommaVariables(startupString, commaVars);

        return LScriptCore.ExecuteScriptUnsafe(startupString, callItemId: packId, quotePaths: true);
    }
}
