#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.LScript;

namespace CDPIUI.ViewModels;

internal static class ConfigMakerPresetDocumentConverter
{
    public static ConfigItem ToConfigItem(ConfigMakerPresetDocument document, string packId, string presetName)
    {
        Dictionary<string, bool> jparams = new(StringComparer.OrdinalIgnoreCase);
        List<string> variables = [];
        Dictionary<string, string> commaVariables = new(StringComparer.OrdinalIgnoreCase);
        List<AvailableVarValues> availableValues = [];

        foreach (ConfigMakerVariableDefinition variable in document.Variables)
        {
            if (variable.StorageKind == ConfigMakerVariableStorageKind.Conditional ||
                variable.Kind == ConfigMakerVariableKind.Switch)
            {
                string parameterName = string.IsNullOrWhiteSpace(variable.InternalParameterName)
                    ? CreateInternalParameterName(variable.Name, variable.Id)
                    : variable.InternalParameterName;
                jparams[parameterName] = variable.IsSwitchEnabled;
                variables.Add(
                    $"%{variable.Name}%=$LOCALCONDITION({parameterName}==true ? " +
                    $"{variable.OnValue} $SEPARATOR {variable.OffValue})");
                continue;
            }

            if (variable.StorageKind == ConfigMakerVariableStorageKind.Expression)
            {
                variables.Add($"%{variable.Name}%={variable.Value}");
                continue;
            }

            commaVariables[variable.Name] = variable.Value;
            if (variable.Kind == ConfigMakerVariableKind.Choice ||
                variable.Values.Count > 0 ||
                !string.IsNullOrWhiteSpace(variable.Description))
            {
                int selectedIndex = variable.Values.IndexOf(variable.Value);
                availableValues.Add(new AvailableVarValues
                {
                    VarName = variable.Name,
                    Comment = variable.Description,
                    CurrentValueIndex = selectedIndex,
                    Values = variable.Values.Distinct(StringComparer.Ordinal).ToList(),
                });
            }
        }

        ConfigMakerPresetMetadata? metadata = document.Variables.Count > 0 || document.Resources.Count > 0
            ? CreateMetadata(document)
            : null;

        return new ConfigItem
        {
            packId = packId,
            meta = string.IsNullOrWhiteSpace(document.Meta) ? null : document.Meta,
            not_converted_name = presetName,
            name = presetName,
            target = string.IsNullOrWhiteSpace(document.TargetVersion)
                ? [document.ComponentId]
                : [document.ComponentId, document.TargetVersion],
            jparams = jparams,
            variables = variables,
            commaVars = commaVariables.Count == 0 ? null : commaVariables,
            availableCommaVarsValues = availableValues.Count == 0 ? null : availableValues,
            startup_string = document.CommandText,
            configMaker = metadata,
        };
    }

    public static ConfigMakerPresetDocument FromConfigItem(ConfigItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ConfigMakerPresetDocument document = new()
        {
            ComponentId = item.target?.FirstOrDefault() ?? string.Empty,
            Name = item.not_converted_name ?? item.name ?? string.Empty,
            CommandText = item.startup_string ?? string.Empty,
            PackId = item.packId,
            FileName = item.file_name,
            Meta = item.meta,
            TargetVersion = item.target?.ElementAtOrDefault(1),
        };

        if (item.configMaker?.variables is { Count: > 0 })
        {
            foreach (ConfigMakerVariableMetadata metadata in item.configMaker.variables)
            {
                document.Variables.Add(FromMetadata(metadata));
            }
        }
        else
        {
            LoadLegacyVariables(document, item);
        }

        foreach (ConfigMakerResourceMetadata metadata in item.configMaker?.resources ?? [])
        {
            if (string.IsNullOrWhiteSpace(metadata.alias) || string.IsNullOrWhiteSpace(metadata.path))
            {
                continue;
            }
            document.Resources.Add(new ConfigMakerPresetResource
            {
                Alias = metadata.alias,
                Path = metadata.path,
                Kind = ParseResourceKind(metadata.kind),
                IsBuiltIn = metadata.isBuiltIn,
            });
        }
        document.CommandText = RestoreResourceReferences(
            document.CommandText,
            item.configMaker?.resources ?? []);
        RestoreVariableResourceReferences(
            document.Variables,
            item.configMaker?.resources ?? []);

        return document;
    }

    private static ConfigMakerPresetMetadata CreateMetadata(ConfigMakerPresetDocument document) => new()
    {
        schemaVersion = 1,
        variables = document.Variables.Count == 0 ? null : document.Variables.Select(variable => new ConfigMakerVariableMetadata
        {
            id = variable.Id,
            name = variable.Name,
            kind = variable.Kind.ToString(),
            storageKind = variable.StorageKind.ToString(),
            value = string.IsNullOrEmpty(variable.Value) ? null : variable.Value,
            description = string.IsNullOrEmpty(variable.Description) ? null : variable.Description,
            values = variable.Values.Count == 0 ? null : variable.Values.ToList(),
            onValue = string.IsNullOrEmpty(variable.OnValue) ? null : variable.OnValue,
            offValue = string.IsNullOrEmpty(variable.OffValue) ? null : variable.OffValue,
            internalParameterName = string.IsNullOrEmpty(variable.InternalParameterName)
                ? null
                : variable.InternalParameterName,
            isSwitchEnabled = variable.IsSwitchEnabled,
        }).ToList(),
        resources = document.Resources.Count == 0 ? null : document.Resources.Select(resource => new ConfigMakerResourceMetadata
        {
            alias = resource.Alias,
            path = resource.Path,
            kind = resource.Kind.ToString(),
            isBuiltIn = resource.IsBuiltIn,
        }).ToList(),
    };

    private static ConfigMakerVariableDefinition FromMetadata(ConfigMakerVariableMetadata metadata)
    {
        ConfigMakerVariableDefinition variable = new()
        {
            Id = string.IsNullOrWhiteSpace(metadata.id)
                ? Guid.NewGuid().ToString("N")
                : metadata.id,
            Name = metadata.name ?? string.Empty,
            Kind = Enum.TryParse(metadata.kind, ignoreCase: true, out ConfigMakerVariableKind kind)
                ? kind
                : ConfigMakerVariableKind.Text,
            StorageKind = Enum.TryParse(
                metadata.storageKind,
                ignoreCase: true,
                out ConfigMakerVariableStorageKind storageKind)
                ? storageKind
                : ConfigMakerVariableStorageKind.Direct,
            Value = metadata.value ?? string.Empty,
            Description = metadata.description ?? string.Empty,
            OnValue = metadata.onValue ?? string.Empty,
            OffValue = metadata.offValue ?? string.Empty,
            InternalParameterName = metadata.internalParameterName ?? string.Empty,
            IsSwitchEnabled = metadata.isSwitchEnabled,
        };
        foreach (string value in metadata.values ?? [])
        {
            variable.Values.Add(value);
        }
        return variable;
    }

    private static void LoadLegacyVariables(ConfigMakerPresetDocument document, ConfigItem item)
    {
        foreach ((string variableName, string value) in item.commaVars ?? [])
        {
            AvailableVarValues? values = item.availableCommaVarsValues?.FirstOrDefault(candidate =>
                string.Equals(candidate.VarName, variableName, StringComparison.OrdinalIgnoreCase));
            ConfigMakerVariableDefinition variable = new()
            {
                Name = variableName,
                Kind = values?.Values is { Count: > 0 }
                    ? ConfigMakerVariableKind.Choice
                    : ConfigMakerVariableKind.Text,
                StorageKind = ConfigMakerVariableStorageKind.Direct,
                Value = value,
                Description = values?.Comment ?? string.Empty,
            };
            foreach (string availableValue in values?.Values ?? [])
            {
                variable.Values.Add(availableValue);
            }
            document.Variables.Add(variable);
        }

        foreach (string expression in item.variables ?? [])
        {
            Tuple<string, string, string, string>? condition =
                LScriptLangHelper.GetNameOnOffValuesFromConditionString(expression);
            if (condition != null && !string.IsNullOrWhiteSpace(condition.Item2))
            {
                document.Variables.Add(new ConfigMakerVariableDefinition
                {
                    Name = condition.Item2,
                    Kind = ConfigMakerVariableKind.Switch,
                    StorageKind = ConfigMakerVariableStorageKind.Conditional,
                    InternalParameterName = condition.Item1,
                    OnValue = condition.Item3,
                    OffValue = condition.Item4,
                    IsSwitchEnabled = item.jparams?.GetValueOrDefault(condition.Item1) ?? false,
                });
                continue;
            }

            Match match = Regex.Match(expression, "^%(?<name>[A-Za-z0-9_]+)%=(?<value>.*)$");
            if (match.Success)
            {
                document.Variables.Add(new ConfigMakerVariableDefinition
                {
                    Name = match.Groups["name"].Value,
                    Kind = ConfigMakerVariableKind.Text,
                    StorageKind = ConfigMakerVariableStorageKind.Expression,
                    Value = match.Groups["value"].Value,
                });
            }
        }
    }

    private static string CreateInternalParameterName(string name, string id) =>
        $"{name}_var_{id[..Math.Min(8, id.Length)]}";

    private static ConfigMakerResourceKind ParseResourceKind(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out ConfigMakerResourceKind kind)
            ? kind
            : ConfigMakerResourceKind.Other;

    private static string RestoreResourceReferences(
        string commandText,
        IEnumerable<ConfigMakerResourceMetadata> resources) =>
        ConfigFileReferences.RestorePresetReferences(commandText, resources);

    private static void RestoreVariableResourceReferences(
        IEnumerable<ConfigMakerVariableDefinition> variables,
        IEnumerable<ConfigMakerResourceMetadata> resources)
    {
        ConfigMakerResourceMetadata[] resourceSnapshot = resources.ToArray();
        foreach (ConfigMakerVariableDefinition variable in variables)
        {
            variable.Value = RestoreResourceReferences(variable.Value, resourceSnapshot);
            variable.OnValue = RestoreResourceReferences(variable.OnValue, resourceSnapshot);
            variable.OffValue = RestoreResourceReferences(variable.OffValue, resourceSnapshot);
            for (int index = 0; index < variable.Values.Count; index++)
            {
                variable.Values[index] = RestoreResourceReferences(
                    variable.Values[index],
                    resourceSnapshot);
            }
        }
    }
}
