#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerSimpleDesignerBackend
{
    bool IsSupported(string componentId);
    bool TryLoad(string componentId, ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups);
    string Parse(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string command);
    string Build(ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string additional);
}

internal sealed record ConfigMakerDesignerCommands(ICommand Text, ICommand Bool, ICommand SelectedGuid);

internal enum ConfigMakerDesignerNotice
{
    MissingAnnotation,
    LoadFailed,
}
