using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// Builds a Config from the launch arguments of a generic command file and the variables they use.
/// </summary>
internal static class GenericConfigItemFactory
{
    public const string ConfigMeta = "pUC:v1.0";

    public static ConfigItem Create(
        string name,
        string arguments,
        IReadOnlyDictionary<string, GenericBatchVariable> variables,
        ConfigImportTarget target)
    {
        Dictionary<string, string>? commaVariables = variables.Count == 0
            ? null
            : variables.ToDictionary(
                pair => pair.Key,
                pair => GenericBatchExpander.TranslateBatchValue(pair.Value.Value),
                StringComparer.OrdinalIgnoreCase);
        commaVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddUnrecognizedCommaVariables(arguments, variables, commaVariables);
        if (commaVariables.Count == 0)
            commaVariables = null;
        List<AvailableVarValues>? alternatives = variables.Values
            .Where(variable => variable.Alternatives.Count > 0)
            .Select(variable => new AvailableVarValues
            {
                VarName = variable.Name,
                CurrentValueIndex = 0,
                Values = variable.GetSelectableValues()
                    .Select(GenericBatchExpander.TranslateBatchValue)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();

        return new ConfigItem
        {
            meta = ConfigMeta,
            name = name,
            target = [target.ComponentId, ConfigImportTargets.ResolveVersion(target)],
            commaVars = commaVariables,
            availableCommaVarsValues = alternatives is { Count: > 0 } ? alternatives : null,
            jparams = [],
            variables = [],
            startup_string = GenericBatchSyntax.NormalizeWhitespace(GenericBatchExpander.TranslateBatchValue(arguments)),
        };
    }

    private static void AddUnrecognizedCommaVariables(
        string arguments,
        IReadOnlyDictionary<string, GenericBatchVariable> variables,
        IDictionary<string, string> commaVariables)
    {
        var recognized = variables.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> values = new[] { arguments }
            .Concat(variables.Values.Select(variable => variable.Value))
            .Concat(variables.Values.SelectMany(variable => variable.Alternatives));

        foreach (string value in values)
        {
            foreach (string name in GenericBatchExpander.GetVariableNames(value))
            {
                if (!recognized.Contains(name) && !GenericBatchExpander.IsBuiltinBatchVariable(name))
                    commaVariables.TryAdd(name, "$EMPTY");
            }
        }
    }
}
