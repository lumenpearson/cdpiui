namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// A batch variable of a generic command file: its value and the commented-out alternatives.
/// </summary>
internal sealed class GenericBatchVariable(string name, string value)
{
    public string Name { get; } = name;
    public string Value { get; set; } = value;
    public List<string> Alternatives { get; } = [];

    public void AddAlternative(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            !string.Equals(Value, value, StringComparison.Ordinal) &&
            !Alternatives.Contains(value, StringComparer.Ordinal))
        {
            Alternatives.Add(value);
        }
    }

    public IEnumerable<string> GetSelectableValues()
    {
        if (!string.IsNullOrWhiteSpace(Value))
            yield return Value;
        foreach (string alternative in Alternatives)
            yield return alternative;
    }

    public GenericBatchVariable Clone()
    {
        var clone = new GenericBatchVariable(Name, Value);
        clone.Alternatives.AddRange(Alternatives);
        return clone;
    }
}
