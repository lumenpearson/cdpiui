#nullable enable

namespace CDPIUI.ViewModels;

// Retains the original settings keys, value types and error handling.
internal sealed class BlockCheck2OptionsSettings : IBlockCheck2OptionsSettings
{
    public int ReadInt(string key, int defaultValue) => BlockCheck2SettingsStore.ReadInt(key, defaultValue);
    public bool ReadBool(string key, bool defaultValue) => BlockCheck2SettingsStore.ReadBool(key, defaultValue);
    public double ReadDouble(string key, double defaultValue) => BlockCheck2SettingsStore.ReadDouble(key, defaultValue);
    public void Save(string? key, int value) => BlockCheck2SettingsStore.Save(key, value);
    public void Save(string? key, bool value) => BlockCheck2SettingsStore.Save(key, value);
    public void Save(string? key, double value) => BlockCheck2SettingsStore.Save(key, value);
}
