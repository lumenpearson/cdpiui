#nullable enable

using System.Threading.Tasks;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerDocumentStorage
{
    Task<ConfigMakerPresetSaveResult> SaveAsync(string presetName, ConfigMakerPresetDocument document);
    Task<ConfigMakerPresetSaveResult> OverwriteAsync(ConfigMakerPresetDocument document);
}
