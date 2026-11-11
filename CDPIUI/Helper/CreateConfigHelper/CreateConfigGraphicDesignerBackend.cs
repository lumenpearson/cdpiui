#nullable enable

using System.Collections.ObjectModel;
using System.IO;
using CDPIUI.Core.Store.Database;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// Specific factories preserve the config page's own branch/cache policy.
internal sealed class CreateConfigGraphicDesignerBackend : ICreateConfigGraphicDesignerBackend
{
    public void LoadGoodbye(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups) =>
        GraphicDesignerHelper.LoadGoodbyeDPIDesignerConfig(items, groups);

    public void LoadSpoof(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups) =>
        GraphicDesignerHelper.LoadSpoofDPIDesignerConfig(items, groups);

    public bool TryLoadNoDpi(string componentId, ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups)
    {
        string directory = DatabaseHelper.Instance.GetItemById(componentId)?.Directory ?? string.Empty;
        string annotation = Path.Combine(directory, "edannotationfile.xml");
        if (!File.Exists(annotation))
            return false;
        GraphicDesignerHelper.XML_LoadDesignerConfig(annotation, "nodpi", items, groups);
        return true;
    }

    public string Parse(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string text) =>
        GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(items, groups, text);

    public string Build(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string additional) =>
        GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(items, groups, additional);
}
