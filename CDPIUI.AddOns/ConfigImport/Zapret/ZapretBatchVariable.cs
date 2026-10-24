namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// A batch variable seen during the static analysis: its current value, the commented-out
/// alternatives and every value it was assigned.
/// </summary>
internal sealed class ZapretBatchVariable(string name, string value)
{
    public string Name { get; } = name;

    public string Value { get; set; } = value;

    public List<string> Alternatives { get; } = [];

    public List<string> ObservedValues { get; } = string.IsNullOrEmpty(value) ? [] : [value];

    public bool IsConditionalCandidate { get; set; }

    public void AddAlternative(string alternative)
    {
        if (!string.IsNullOrEmpty(alternative) &&
            !string.Equals(Value, alternative, StringComparison.Ordinal) &&
            !Alternatives.Contains(alternative, StringComparer.Ordinal))
        {
            Alternatives.Add(alternative);
        }
    }

    public void Observe(string observedValue)
    {
        if (!string.IsNullOrEmpty(observedValue) &&
            !ObservedValues.Contains(observedValue, StringComparer.Ordinal))
        {
            ObservedValues.Add(observedValue);
        }
    }

    public IEnumerable<string> GetSelectableValues() =>
        new[] { Value }.Concat(Alternatives);

    public ZapretBatchVariable Clone()
    {
        var result = new ZapretBatchVariable(Name, Value)
        {
            IsConditionalCandidate = IsConditionalCandidate,
        };
        result.Alternatives.AddRange(Alternatives);
        result.ObservedValues.Clear();
        result.ObservedValues.AddRange(ObservedValues);
        return result;
    }
}
