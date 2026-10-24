using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CDPIUI.Core;
using CDPIUI.Core.Features;
using CDPIUI.Shared;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// Applies the GoodbyeDPI-UI settings that have a CDPI UI counterpart.
/// </summary>
internal static class MigrationSettingsImporter
{
    /// <returns>The number of settings that were applied.</returns>
    internal static int Apply(
        MigrationDataDocument data,
        IReadOnlyDictionary<string, string> presetFilesBySource,
        ICollection<MigrationImportIssue> issues,
        CancellationToken cancellationToken)
    {
        Dictionary<string, MigrationSetting> candidates = data.Settings
            .Where(setting => setting.ImportPolicy == "candidate" &&
                !string.IsNullOrWhiteSpace(setting.SemanticKey) &&
                !string.Equals(
                    Path.GetFileName(setting.SourceRelativePath),
                    "FluentUI-Gallery.ini",
                    StringComparison.OrdinalIgnoreCase))
            .GroupBy(setting => setting.SemanticKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        int applied = ApplyApplicationSettings(candidates, issues, cancellationToken);
        applied += ApplyActiveSelection(data, candidates, presetFilesBySource, issues);
        return applied;
    }

    private static int ApplyApplicationSettings(
        Dictionary<string, MigrationSetting> candidates,
        ICollection<MigrationImportIssue> issues,
        CancellationToken cancellationToken)
    {
        int applied = 0;

        foreach ((string semanticKey, MigrationSetting setting) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                switch (semanticKey)
                {
                    case "ui.theme":
                        // A theme already chosen in CDPI UI wins over the old one; "Dark" is the
                        // value of a new settings file (Template/Settings/Settings.xml).
                        string currentTheme = SettingsManager.Instance.GetValue<string>("APPEARANCE", "Theme") ?? string.Empty;
                        if (!currentTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase) &&
                            !currentTheme.Equals("Default", StringComparison.OrdinalIgnoreCase))
                            break;
                        string theme = (setting.Value ?? string.Empty).Trim().ToLowerInvariant() switch
                        {
                            "dark" or "1" => "Dark",
                            "light" or "0" => "Light",
                            _ => "Default"
                        };
                        SettingsManager.Instance.SetValue("APPEARANCE", "Theme", theme);
                        applied++;
                        break;
                    case "updates.source":
                        string source = (setting.Value ?? string.Empty).Contains(
                            "gitlab", StringComparison.OrdinalIgnoreCase) ? "GitLab" : "GitHub";
                        SettingsManager.Instance.SetValue("STORE", "versionControlType", source);
                        applied++;
                        break;
                    case "startup.start-minimized":
                    case "startup.minimize-to-tray":
                        if (TryParseBoolean(setting.Value, out bool minimized))
                        {
                            SettingsManager.Instance.SetValue("APPEARANCE", "hideToTrayOnStartup", minimized);
                            applied++;
                        }
                        break;
                    case "startup.autostart":
                        // Only adds: an autorun the user set up in CDPI UI is not removed by the import.
                        if (TryParseBoolean(setting.Value, out bool autostart) && autostart)
                        {
                            ApplicationAutorunManager.AddToAutorun();
                            applied++;
                        }
                        break;
                }
            }
            catch (Exception exception)
            {
                MigrationImportIssueLog.Add(issues, "setting", semanticKey, exception);
            }
        }
        return applied;
    }

    /// <summary>
    /// Selects the component and the preset that GoodbyeDPI-UI used last.
    /// </summary>
    private static int ApplyActiveSelection(
        MigrationDataDocument data,
        Dictionary<string, MigrationSetting> candidates,
        IReadOnlyDictionary<string, string> presetFilesBySource,
        ICollection<MigrationImportIssue> issues)
    {
        int applied = 0;
        try
        {
            MigrationPreset? selectedPreset = null;
            if (candidates.TryGetValue("runtime.active-preset", out MigrationSetting? presetSetting) &&
                !string.IsNullOrWhiteSpace(presetSetting.Value))
            {
                string value = presetSetting.Value.Trim();
                selectedPreset = data.Presets.FirstOrDefault(preset =>
                    string.Equals(preset.Id, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(preset.Name, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(preset.SourceRelativePath, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileNameWithoutExtension(preset.SourceRelativePath), value, StringComparison.OrdinalIgnoreCase));
            }

            MigrationComponentRequirement? activeComponent = selectedPreset == null
                ? MapLegacyComponent(candidates.GetValueOrDefault("runtime.active-component")?.Value)
                : GoodbyeDpiComponentMapper.Map(selectedPreset);
            if (activeComponent != null)
            {
                SettingsManager.Instance.SetValue("COMPONENTS", "nowUsed", activeComponent.InstallItemId);
                applied++;
            }
            if (selectedPreset != null && activeComponent != null &&
                presetFilesBySource.TryGetValue(selectedPreset.SourceRelativePath!, out string? configFile))
            {
                SettingsManager.Instance.SetValue(
                    ["CONFIGS", activeComponent.InstallItemId], "configId",
                    SharedConstants.MigratedGoodbyeDpiUiStoreItemId);
                SettingsManager.Instance.SetValue(
                    ["CONFIGS", activeComponent.InstallItemId], "configFile", configFile);
                applied++;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MigrationImportIssueLog.Add(issues, "setting", "runtime.selection", exception);
        }
        return applied;
    }

    private static MigrationComponentRequirement? MapLegacyComponent(string? value)
    {
        string source = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (source.Length == 0)
            return null;
        MigrationPreset placeholder = new()
        {
            Component = source switch
            {
                var item when item.Contains("zapret") => "zapret",
                var item when item.Contains("byedpi") => "byedpi",
                var item when item.Contains("spoof") => "spoofdpi",
                var item when item.Contains("nodpi") => "nodpi",
                _ => "goodbyedpi"
            }
        };
        return GoodbyeDpiComponentMapper.Map(placeholder);
    }

    private static bool TryParseBoolean(string? value, out bool result)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "true" or "1" or "yes" or "on")
        {
            result = true;
            return true;
        }
        if (normalized is "false" or "0" or "no" or "off")
        {
            result = false;
            return true;
        }
        result = false;
        return false;
    }
}
