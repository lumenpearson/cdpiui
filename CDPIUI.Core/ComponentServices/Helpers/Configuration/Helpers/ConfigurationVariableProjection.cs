namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;

/// <summary>Builds owned read models from current preset variable definitions.</summary>
internal static class ConfigurationVariableProjection
{
    public static List<VariableItem> ToVariableItems(Dictionary<string, bool> jparams)
    {
        List<VariableItem> variables = [];

        foreach (var variable in jparams)
        {
            VariableItem variableItem = new()
            {
                name = variable.Key,
                value = variable.Value,
            };
            variables.Add(variableItem);
        }

        return variables;
    }

    public static List<CommaVariableItem> ToCommaVariableItems(ConfigItem configItem)
    {
        var result = new List<CommaVariableItem>();
        foreach (var commaVariable in configItem.commaVars!)
        {
            AvailableVarValues? availableValues = configItem.availableCommaVarsValues?.FirstOrDefault(
                item => string.Equals(item.VarName, commaVariable.Key, StringComparison.OrdinalIgnoreCase));
            List<string> values = (availableValues?.Values ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToList();

            result.Add(new CommaVariableItem
            {
                name = commaVariable.Key,
                comment = availableValues?.Comment,
                value = commaVariable.Value,
                values = values,
            });
        }

        return result;
    }
}
