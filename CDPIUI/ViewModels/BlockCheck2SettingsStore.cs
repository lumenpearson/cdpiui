using System;
using CDPIUI.Core;
using CDPIUI.Core.Basic;

namespace CDPIUI.ViewModels;

/// <summary>
/// Reads and writes the BlockCheck2 page options in the settings file. A failed read returns the
/// default value and a failed write is only logged, so the page keeps working with broken settings.
/// </summary>
internal static class BlockCheck2SettingsStore
{
    private const string SettingsGroup = "BLOCKCHECK";

    public static int ReadInt(string key, int defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogFailure(key, exception);
            return defaultValue;
        }
    }

    public static double ReadDouble(string key, double defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogFailure(key, exception);
            return defaultValue;
        }
    }

    public static bool ReadBool(string key, bool defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogFailure(key, exception);
            return defaultValue;
        }
    }

    public static void Save<T>(string? key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            SettingsManager.Instance.SetValue(SettingsGroup, key, value);
        }
        catch (Exception exception)
        {
            LogFailure(key, exception);
        }
    }

    private static void LogFailure(string key, Exception exception) =>
        Logger.Instance.CreateWarningLog(
            nameof(BlockCheck2ViewModel),
            $"Cannot read or save BlockCheck2 setting '{key}': {exception.Message}");
}
