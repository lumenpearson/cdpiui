namespace CDPIUI.Controls.CreateConfigHelper;

public sealed record ConfigMakerPresetFileInfo(
    string Name,
    string Path,
    string Folder,
    ConfigMakerPresetFileKind Kind = ConfigMakerPresetFileKind.SiteList,
    string OptionName = "",
    bool IsAttachedResource = false);
