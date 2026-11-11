using System;
using System.Collections.ObjectModel;
using System.IO;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// Keeps the existing configuration factories/codec and resolves services only when loading.
internal sealed class ConfigMakerSimpleDesignerBackend : IConfigMakerSimpleDesignerBackend
{
    public bool IsSupported(string componentId) =>
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.GoodbyeDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.SpoofDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.NoDPI], StringComparison.OrdinalIgnoreCase);

    public bool TryLoad(string componentId, ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups)
    {
        if (string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.GoodbyeDPI], StringComparison.OrdinalIgnoreCase))
        {
            GraphicDesignerHelper.LoadGoodbyeDPIDesignerConfig(items, groups);
            return true;
        }
        if (string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.SpoofDPI], StringComparison.OrdinalIgnoreCase))
        {
            GraphicDesignerHelper.LoadSpoofDPIDesignerConfig(items, groups);
            return true;
        }

        string componentDirectory = DatabaseHelper.Instance.GetItemById(componentId)?.Directory ?? string.Empty;
        string annotationPath = Path.Combine(componentDirectory, "edannotationfile.xml");
        if (!File.Exists(annotationPath))
        {
            return false;
        }
        GraphicDesignerHelper.XML_LoadDesignerConfig(annotationPath, "nodpi", items, groups);
        return true;
    }

    public string Parse(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string command) =>
        GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(items, groups, command);

    public string Build(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string additional) =>
        GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(items, groups, additional);
}
