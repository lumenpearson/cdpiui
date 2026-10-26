using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Expands batch variables and path modifiers statically and translates batch values to the
/// Config syntax.
/// </summary>
internal static class ZapretBatchExpander
{
    public static string ExpandBatchValue(
        string value,
        string batchFile,
        IReadOnlyDictionary<string, ZapretBatchVariable> variables,
        int depth = 0)
    {
        if (depth > 16)
            return value;

        string directory = Path.GetDirectoryName(batchFile)! + Path.DirectorySeparatorChar;
        // "%%" is a literal percent sign; it is kept out of the variable expansion below.
        const string PercentPlaceholder = "\u0000";
        string result = value
            .Replace("%%", PercentPlaceholder, StringComparison.Ordinal)
            .Replace("$GETCURRENTDIR()/", directory, StringComparison.OrdinalIgnoreCase)
            .Replace("$GETCURRENTDIR()\\", directory, StringComparison.OrdinalIgnoreCase);
        result = ZapretSyntax.BatchFileModifierRegex().Replace(result, match => ExpandFileModifiers(match.Groups["modifiers"].Value, batchFile));
        result = result.Replace("%0", batchFile, StringComparison.OrdinalIgnoreCase);

        string Replace(Match match)
        {
            string name = match.Groups["name"].Value;
            if (variables.TryGetValue(name, out ZapretBatchVariable? variable))
                return ExpandBatchValue(variable.Value, batchFile, variables, depth + 1);

            return Environment.GetEnvironmentVariable(name) ?? match.Value;
        }

        result = ZapretSyntax.BatchVariableRegex().Replace(result, Replace);
        result = ZapretSyntax.DelayedVariableRegex().Replace(result, Replace);
        return depth == 0 ? result.Replace(PercentPlaceholder, "%", StringComparison.Ordinal) : result;
    }

    /// <summary>
    /// Expands the %~...0 modifiers of the batch file path: f (full path), d (drive), p (directory),
    /// n (name) and x (extension), combined as cmd does
    /// </summary>
    public static string ExpandFileModifiers(string modifiers, string batchFile)
    {
        string fullPath = Path.GetFullPath(batchFile);
        modifiers = modifiers.ToLowerInvariant();
        if (modifiers.Contains('f') && !modifiers.Any(c => c is 'd' or 'p' or 'n' or 'x'))
            return fullPath;

        string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        string drive = root.TrimEnd('\\', '/');
        string path = directory[root.Length..];
        path = path.Length == 0 ? "\\" : "\\" + path.TrimStart('\\') + "\\";

        var result = new StringBuilder();
        if (modifiers.Contains('d'))
            result.Append(drive);
        if (modifiers.Contains('p'))
            result.Append(path);
        if (modifiers.Contains('n'))
            result.Append(Path.GetFileNameWithoutExtension(fullPath));
        if (modifiers.Contains('x'))
            result.Append(Path.GetExtension(fullPath));
        return result.ToString();
    }

    public static string TranslateBatchValueToConfig(string value) =>
        value.Replace("%~dp0", "$GETCURRENTDIR()/", StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<string> GetReferencedVariableNames(string value)
    {
        foreach (Match match in ZapretSyntax.BatchVariableRegex().Matches(value))
        {
            string name = match.Groups["name"].Value;
            if (!name.StartsWith('~'))
                yield return name;
        }

        foreach (Match match in ZapretSyntax.DelayedVariableRegex().Matches(value))
            yield return match.Groups["name"].Value;
    }

    public static string NormalizeDelayedVariableSyntax(string value) =>
        ZapretSyntax.DelayedVariableRegex().Replace(value, match => $"%{match.Groups["name"].Value}%");

    public static void NormalizeDelayedVariableSyntax(IEnumerable<ZapretBatchVariable> variables)
    {
        foreach (ZapretBatchVariable variable in variables)
        {
            variable.Value = NormalizeDelayedVariableSyntax(variable.Value);
            for (int index = 0; index < variable.Alternatives.Count; index++)
            {
                variable.Alternatives[index] =
                    NormalizeDelayedVariableSyntax(variable.Alternatives[index]);
            }
            for (int index = 0; index < variable.ObservedValues.Count; index++)
            {
                variable.ObservedValues[index] =
                    NormalizeDelayedVariableSyntax(variable.ObservedValues[index]);
            }
        }
    }
}
