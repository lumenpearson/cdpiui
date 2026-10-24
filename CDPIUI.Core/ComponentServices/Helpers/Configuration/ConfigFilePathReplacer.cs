using System.Text.RegularExpressions;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>
/// Replaces the paths of the files a preset uses, in its startup string and its variables.
/// </summary>
internal static class ConfigFilePathReplacer
{
    public static ConfigItem ReplaceFilesPath(ConfigItem configItem, Dictionary<string, string> files)
    {
        if (!string.IsNullOrEmpty(configItem.startup_string))
        {
            configItem.startup_string = ReplaceInString(configItem.startup_string, files);
        }

        if (configItem.commaVars != null)
        {
            var keys = configItem.commaVars.Keys.ToList();
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(configItem.commaVars[key]))
                    configItem.commaVars[key] = ReplaceInString(configItem.commaVars[key], files);
            }
        }

        if (configItem.availableCommaVarsValues != null)
        {
            foreach (var varValue in configItem.availableCommaVarsValues)
            {
                if (varValue.Values != null)
                {
                    for (int i = 0; i < varValue.Values.Count; i++)
                    {
                        varValue.Values[i] = ReplaceInString(varValue.Values[i], files);
                    }
                }
            }
        }

        return configItem;
    }

    // Replaces whole paths only, with either separator and with the quotes around them:
    // a substring replacement also changed "mylist.txt" when "list.txt" was replaced, every
    // apostrophe in the line became a quote, and "" (an empty value) became ".
    private static string ReplaceInString(string input, Dictionary<string, string> files)
    {
        if (string.IsNullOrEmpty(input) || files == null || files.Count == 0)
            return input;

        input = input.Replace("%~dp0", "");

        foreach (var kvp in files)
        {
            string oldPath = kvp.Key?.Trim().Trim('"', '\'') ?? string.Empty;
            if (oldPath.Length == 0)
                continue;
            string newPath = $"\"{kvp.Value.Trim().Trim('"')}\"";

            string pathPattern = string.Join("[\\\\/]",
                oldPath.Split('\\', '/').Select(Regex.Escape));
            input = Regex.Replace(
                input,
                $@"(?<![\w.\\/:-])(?<q>[""']?){pathPattern}\k<q>(?![\w.\\/-])",
                _ => newPath,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return input;
    }
}
