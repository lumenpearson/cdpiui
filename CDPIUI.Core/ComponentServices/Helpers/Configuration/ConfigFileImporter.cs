using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.JSON;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>
/// Reads a preset from a file chosen by the user: a preset, an old-format preset or a
/// .bat/.cmd script.
/// </summary>
internal static class ConfigFileImporter
{
    // The item is null when a .json file holds "null".
    public static Tuple<ConfigItem?, bool>? Load(string filepath)
    {
        if (!File.Exists(filepath))
            return null;

        if (Path.GetExtension(filepath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            OldConfigItem oldConfig = JSONConvertor.LoadJson<OldConfigItem>(filepath);
            if (oldConfig != null && oldConfig.custom_parameters != null)
            {
                ConfigItem newConfig = new ConfigItem()
                {
                    meta = "pUC:v1.0",
                    name = Path.GetFileNameWithoutExtension(filepath),
                    target = null,
                    jparams = [],
                    variables = [],
                    startup_string = oldConfig.custom_parameters,
                };
                return Tuple.Create<ConfigItem?, bool>(newConfig, true);
            }
            return Tuple.Create(JSONConvertor.LoadJson<ConfigItem>(filepath), true);
        }
        else if (Path.GetExtension(filepath).Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(filepath).Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var (configItem, errorHappens) = BatConfigConverter.Convert(filepath);

                return Tuple.Create<ConfigItem?, bool>(configItem, errorHappens);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(ConfigurationService), $"Error happens: {ex}");
                return null;
            }
        }

        return null;
    }
}
