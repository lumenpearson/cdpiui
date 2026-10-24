namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Operations on the case-insensitive variable tables of the static batch analysis.
/// </summary>
internal static class ZapretVariableTable
{
    public static Dictionary<string, ZapretBatchVariable> Create() =>
        new(StringComparer.OrdinalIgnoreCase);

    public static void Set(
        IDictionary<string, ZapretBatchVariable> variables,
        string name,
        string value,
        bool isAlternative,
        bool isConditionalCandidate = false)
    {
        if (!variables.TryGetValue(name, out ZapretBatchVariable? variable))
        {
            variable = new ZapretBatchVariable(name, isAlternative ? string.Empty : value);
            variables[name] = variable;
        }

        variable.IsConditionalCandidate |= isConditionalCandidate;
        variable.Observe(value);

        if (isAlternative)
        {
            variable.AddAlternative(value);
        }
        else
        {
            variable.AddAlternative(variable.Value);
            variable.Value = value;
        }
    }

    public static Dictionary<string, ZapretBatchVariable> Clone(
        IReadOnlyDictionary<string, ZapretBatchVariable> variables) =>
        variables.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.OrdinalIgnoreCase);

    public static void Merge(
        IReadOnlyDictionary<string, ZapretBatchVariable> source,
        IDictionary<string, ZapretBatchVariable> destination)
    {
        foreach (var pair in source)
        {
            if (!destination.TryGetValue(pair.Key, out ZapretBatchVariable? existing))
            {
                destination[pair.Key] = pair.Value.Clone();
                continue;
            }

            foreach (string alternative in pair.Value.Alternatives.Append(pair.Value.Value))
                existing.AddAlternative(alternative);

            existing.IsConditionalCandidate |= pair.Value.IsConditionalCandidate;
            foreach (string observedValue in pair.Value.ObservedValues)
                existing.Observe(observedValue);
        }
    }
}
