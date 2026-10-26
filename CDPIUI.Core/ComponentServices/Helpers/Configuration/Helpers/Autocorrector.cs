using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;

public static class Autocorrector
{
    public static string FindAutoCorrectPath(string filePath, ConfigItem configItem, string configPath)
    {
        try
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(filePath), "autohostlist", StringComparison.OrdinalIgnoreCase))
            {
                return filePath;
            }
            if (configItem != null && configItem.packId != null && configItem.jparams != null)
            {
                string _filePath = LScript.LScriptCore.ExecuteScriptUnsafe(
                    ConfigurationService.ReplaceVariables(
                        filePath,
                        ConfigurationService.GetReadyToUseVariables(
                            configItem.packId,
                            configItem.variables!,
                            configItem.jparams)!),
                    callItemId: configItem.packId);

                _filePath = LScript.LScriptCore.ExecuteScriptUnsafe(
                    ConfigurationService.ReplaceCommaVariables(
                        _filePath,
                        configItem.commaVars!),
                    callItemId: configItem.packId);

                if (Path.Exists(_filePath))
                    return _filePath;
            }
            if (File.Exists(filePath))
            {
                return filePath;
            }

            if (File.Exists(Path.Combine(Path.GetDirectoryName(configPath)!, filePath)))
            {
                return Path.Combine(Path.GetDirectoryName(configPath)!, filePath);
            }

            // One item whose folder is gone must not end the search for all others.
            foreach (string type in new[] { "configlist", "component", "addon" })
            {
                foreach (var item in DatabaseHelper.Instance.GetItemsByType(type))
                {
                    string? found = FindInItem(item.Directory, filePath);
                    if (found != null)
                        return found;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(Autocorrector), $"Cannot find a replacement for \"{filePath}\": {ex.Message}");
        }
        return string.Empty;
    }

    private static string? FindInItem(string? itemPath, string filePath)
    {
        if (string.IsNullOrEmpty(itemPath) || !Directory.Exists(itemPath))
            return null;
        try
        {
            string fileName = Path.GetFileName(filePath);
            return Directory.EnumerateFiles(itemPath, $"*{Path.GetExtension(filePath)}", SearchOption.AllDirectories)
                .FirstOrDefault(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
