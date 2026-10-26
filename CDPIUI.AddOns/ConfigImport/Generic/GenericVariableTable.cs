namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// Operations on the case-insensitive variable tables of the generic command-file import.
/// </summary>
internal static class GenericVariableTable
{
    public static Dictionary<string, GenericBatchVariable> Create() =>
        new(StringComparer.OrdinalIgnoreCase);

    public static void Set(
        IDictionary<string, GenericBatchVariable> variables,
        string name,
        string value,
        bool alternative)
    {
        if (!variables.TryGetValue(name, out GenericBatchVariable? variable))
        {
            variable = new(name, alternative ? string.Empty : value);
            variables[name] = variable;
        }
        if (alternative)
            variable.AddAlternative(value);
        else
            variable.Value = value;
    }

    public static Dictionary<string, GenericBatchVariable> Clone(
        IReadOnlyDictionary<string, GenericBatchVariable> variables) =>
        variables.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase);
}
