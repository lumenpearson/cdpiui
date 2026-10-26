using System.IO;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Conversions between the editor file kinds, the preset resource kinds and the Core file kinds.
internal static class ConfigMakerPresetFileKinds
{
    public static ConfigMakerResourceKind InferResourceKind(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".txt" or ".list" => ConfigMakerResourceKind.SiteList,
            ".lua" => ConfigMakerResourceKind.Library,
            ".bin" or ".dat" or ".der" or ".pem" => ConfigMakerResourceKind.Payload,
            _ => ConfigMakerResourceKind.Other,
        };

    public static ConfigMakerPresetFileKind ToPresetFileKind(ConfigMakerResourceKind kind) => kind switch
    {
        ConfigMakerResourceKind.Library => ConfigMakerPresetFileKind.Library,
        ConfigMakerResourceKind.Payload or ConfigMakerResourceKind.Other => ConfigMakerPresetFileKind.Payload,
        _ => ConfigMakerPresetFileKind.SiteList,
    };

    public static ConfigMakerResourceKind ToResourceKind(ConfigMakerPresetFileKind kind) =>
        kind switch
        {
            ConfigMakerPresetFileKind.Library => ConfigMakerResourceKind.Library,
            ConfigMakerPresetFileKind.Payload => ConfigMakerResourceKind.Payload,
            _ => ConfigMakerResourceKind.SiteList,
        };

    public static ConfigFileKind ToCoreFileKind(ConfigMakerPresetFileKind kind) => kind switch
    {
        ConfigMakerPresetFileKind.Library => ConfigFileKind.Library,
        ConfigMakerPresetFileKind.Payload => ConfigFileKind.Payload,
        _ => ConfigFileKind.SiteList,
    };

    public static ConfigMakerPresetFileInfo ToEditorFile(ConfigUsedFile file) => file == null ? null : new(
        file.Name,
        file.Path,
        file.Folder,
        file.Kind switch
        {
            ConfigFileKind.Library => ConfigMakerPresetFileKind.Library,
            ConfigFileKind.Payload or ConfigFileKind.Other => ConfigMakerPresetFileKind.Payload,
            _ => ConfigMakerPresetFileKind.SiteList,
        },
        file.OptionName,
        file.IsAttachedResource);
}
