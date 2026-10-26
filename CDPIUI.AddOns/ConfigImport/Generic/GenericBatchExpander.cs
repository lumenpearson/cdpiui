namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// Expands batch variables statically and translates batch values to the Config syntax.
/// </summary>
internal static class GenericBatchExpander
{
    public static string ExpandBatchValue(
        string value,
        string sourcePath,
        IReadOnlyDictionary<string, GenericBatchVariable> variables,
        int depth = 0)
    {
        if (depth > 16)
            return value;

        string directory = Path.GetDirectoryName(sourcePath)! + Path.DirectorySeparatorChar;
        string result = value
            .Replace("%~dp0", directory, StringComparison.OrdinalIgnoreCase)
            .Replace("%~f0", sourcePath, StringComparison.OrdinalIgnoreCase)
            .Replace("%~n0", Path.GetFileNameWithoutExtension(sourcePath), StringComparison.OrdinalIgnoreCase);

        return GenericBatchSyntax.BatchVariableRegex().Replace(result, match =>
        {
            string name = match.Groups["name"].Value;
            if (variables.TryGetValue(name, out GenericBatchVariable? variable))
                return ExpandBatchValue(variable.Value, sourcePath, variables, depth + 1);
            return Environment.GetEnvironmentVariable(name) ?? match.Value;
        });
    }

    public static string TranslateBatchValue(string value) =>
        value.Replace("%~dp0", "$GETCURRENTDIR()/", StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/');

    public static IEnumerable<string> GetVariableNames(string input) =>
        GenericBatchSyntax.BatchVariableRegex().Matches(input)
            .Select(match => match.Groups["name"].Value)
            .Where(name => !name.StartsWith('~'));

    public static bool IsBuiltinBatchVariable(string name) =>
        name.Equals("CD", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ERRORLEVEL", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("DATE", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("TIME", StringComparison.OrdinalIgnoreCase);
}
