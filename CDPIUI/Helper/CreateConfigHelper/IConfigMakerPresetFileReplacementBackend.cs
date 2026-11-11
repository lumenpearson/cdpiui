#nullable enable

using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerPresetFileReplacementBackend
{
    string? TryResolvePath(string sourcePath);
    bool FileExists(string path);
    void CreateEmptyFile(string path);
    bool TryUpdateAttachedResource(ConfigMakerPresetFileInfo file, string fullReplacementPath);
    ConfigMakerPresetFileReplacement? ReplaceReferences(ConfigMakerPresetFileInfo file, string fullReplacementPath);
    IConfigMakerPresetFileAutoCorrector CreateAutoCorrector();
}

internal interface IConfigMakerPresetFileAutoCorrector
{
    bool ShouldSuggestEmptyFile(string missingPath);
    Task<string?> FindReplacementAsync(string missingPath);
}
