#nullable enable

using System.Collections.ObjectModel;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface ICreateConfigGraphicDesignerBackend
{
    void LoadGoodbye(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups);
    void LoadSpoof(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups);
    bool TryLoadNoDpi(string componentId, ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups);
    string Parse(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string text);
    string Build(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string additional);
}
