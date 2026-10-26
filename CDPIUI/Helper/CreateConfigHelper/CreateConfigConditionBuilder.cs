using System;
using System.Collections.Generic;
using System.IO;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// Builds the $LOCALCONDITION variables of CreateNewConfigPage and stores their descriptions
// in the locale file of the pack.
internal static class CreateConfigConditionBuilder
{
    public static Tuple<Dictionary<string, bool>, List<string>> Build(
        IEnumerable<ConditionVariableModel> conditions,
        int secondsSinceEpoch,
        string packId,
        bool saveLocalization)
    {
        Dictionary<string, bool> jparams = new();
        List<string> vars = new();

        string locFile = ConfigurationService.GetDefaultLocalePath(packId);

        if (string.IsNullOrEmpty(locFile))
            throw new FileNotFoundException("Loc file not found");

        if (!Directory.Exists(Path.GetDirectoryName(locFile)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(locFile));
        }

        Dictionary<string, string> localizationDict = [];
        if (File.Exists(locFile))
        {
            localizationDict = JSONConvertor.LoadJson<Dictionary<string, string>>(locFile);
        }
        localizationDict ??= [];

        foreach (var condition in conditions)
        {
            jparams.Add($"{condition.Name}_var_{secondsSinceEpoch}", false);
            vars.Add($"%{condition.Name}%=$LOCALCONDITION({condition.Name}_var_{secondsSinceEpoch}==true ? {condition.OnValue} $SEPARATOR {condition.OffValue})");
            localizationDict[$"{condition.Name}_var_{secondsSinceEpoch}"] = condition.Description;
        }

        // A test run must not leave Name_var_0 entries in the locale file.
        if (saveLocalization)
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(localizationDict);
            File.WriteAllText(locFile, jsonString);
        }

        return new(jparams, vars);
    }
}
