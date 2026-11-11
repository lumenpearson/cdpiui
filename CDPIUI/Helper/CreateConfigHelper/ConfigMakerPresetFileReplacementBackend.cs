#nullable enable

using System.IO;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// Keeps the existing filesystem/resource/autocorrect operations at the production boundary.
internal sealed class ConfigMakerPresetFileReplacementBackend : IConfigMakerPresetFileReplacementBackend
{
    private readonly ConfigMakerPresetResources _resources;

    public ConfigMakerPresetFileReplacementBackend(ConfigMakerPresetResources resources) => _resources = resources;

    public string? TryResolvePath(string path) => _resources.TryResolvePath(path);
    public bool FileExists(string path) => File.Exists(path);
    public void CreateEmptyFile(string path) => ConfigMakerPresetResources.CreateEmptyFile(path);
    public bool TryUpdateAttachedResource(ConfigMakerPresetFileInfo file, string path) => _resources.TryUpdateAttachedResource(file, path);
    public ConfigMakerPresetFileReplacement? ReplaceReferences(ConfigMakerPresetFileInfo file, string path) => _resources.ReplaceReferences(file, path);
    public IConfigMakerPresetFileAutoCorrector CreateAutoCorrector() => new AutoCorrector(_resources);

    private sealed class AutoCorrector : IConfigMakerPresetFileAutoCorrector
    {
        private readonly ConfigMakerPresetResources _resources;
        private readonly ConfigImportAutoCorrector _corrector;

        public AutoCorrector(ConfigMakerPresetResources resources)
        {
            _resources = resources;
            _corrector = new ConfigImportAutoCorrector();
        }

        public bool ShouldSuggestEmptyFile(string path) => _corrector.ShouldSuggestEmptyFile(path);

        public Task<string?> FindReplacementAsync(string path)
        {
            ConfigImportResult result = _resources.CreateAutoCorrectResult(path);
            return Task.Run(() => _corrector.FindReplacement(result, path));
        }
    }
}
