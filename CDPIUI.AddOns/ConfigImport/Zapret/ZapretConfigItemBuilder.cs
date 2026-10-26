using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Turns an imported zapret launch into a Config: switchable variables become jparams,
/// variables with alternatives become comma variables, the rest become plain variables.
/// </summary>
internal static class ZapretConfigItemBuilder
{
    private const string ConfigMeta = "pUC:v1.0";

    public static ConfigItem Build(ZapretImportedPayload payload, string version)
    {
        string targetId = payload.Generation == ZapretConfigGeneration.Winws2
            ? HardcodedItemIds.ComponentIds[Components.Zapret2]
            : HardcodedItemIds.ComponentIds[Components.Zapret];

        var conditionalVariableNames = payload.ConditionalVariables
            .Select(variable => variable.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string>? commaVariables = payload.Variables.Values
            .Where(variable =>
                !conditionalVariableNames.Contains(variable.Name) &&
                variable.Alternatives.Count > 0)
            .ToDictionary(
                variable => variable.Name,
                variable => ZapretBatchExpander.TranslateBatchValueToConfig(variable.Value),
                StringComparer.OrdinalIgnoreCase);
        AddUnrecognizedCommaVariables(payload, commaVariables);
        if (commaVariables.Count == 0)
            commaVariables = null;

        List<AvailableVarValues>? alternatives = payload.Variables.Values
            .Where(variable =>
                !conditionalVariableNames.Contains(variable.Name) &&
                variable.Alternatives.Count > 0)
            .Select(variable => new AvailableVarValues
            {
                VarName = variable.Name,
                CurrentValueIndex = 0,
                Values = variable.GetSelectableValues()
                    .Select(ZapretBatchExpander.TranslateBatchValueToConfig)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();

        var jparams = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var configVariables = new List<string>();
        foreach (ZapretConditionalVariable variable in payload.ConditionalVariables)
        {
            jparams[variable.ParameterName] = variable.IsEnabled;
            configVariables.Add(
                $"%{variable.Name}%=$LOCALCONDITION({variable.ParameterName}==true ? " +
                $"{ZapretBatchExpander.TranslateBatchValueToConfig(variable.EnabledValue)} $SEPARATOR " +
                $"{ZapretBatchExpander.TranslateBatchValueToConfig(variable.DisabledValue)})");
        }

        configVariables.AddRange(payload.Variables.Values
            .Where(variable =>
                !conditionalVariableNames.Contains(variable.Name) &&
                variable.Alternatives.Count == 0)
            .Select(variable =>
                $"%{variable.Name}%={ZapretBatchExpander.TranslateBatchValueToConfig(variable.Value)}"));

        return new ConfigItem
        {
            meta = ConfigMeta,
            name = payload.Name,
            target = [targetId, version],
            commaVars = commaVariables,
            availableCommaVarsValues = alternatives is { Count: > 0 } ? alternatives : null,
            jparams = jparams,
            variables = configVariables,
            startup_string = ZapretSyntax.NormalizeWhitespace(ZapretBatchExpander.TranslateBatchValueToConfig(payload.StartupString)),
        };
    }

    private static void AddUnrecognizedCommaVariables(
        ZapretImportedPayload payload,
        IDictionary<string, string> commaVariables)
    {
        var recognized = payload.Variables.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> values = new[] { payload.StartupString }
            .Concat(payload.Variables.Values.Select(variable => variable.Value))
            .Concat(payload.Variables.Values.SelectMany(variable => variable.Alternatives));

        foreach (string value in values)
        {
            foreach (Match match in ZapretSyntax.BatchVariableRegex().Matches(value))
            {
                string name = match.Groups["name"].Value;
                if (!name.StartsWith('~') && !recognized.Contains(name))
                    commaVariables.TryAdd(name, "$EMPTY");
            }
        }
    }
}
