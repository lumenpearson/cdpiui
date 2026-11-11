#nullable enable

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2OptionsSettings
{
    int ReadInt(string key, int defaultValue);
    bool ReadBool(string key, bool defaultValue);
    double ReadDouble(string key, double defaultValue);
    void Save(string? key, int value);
    void Save(string? key, bool value);
    void Save(string? key, double value);
}
