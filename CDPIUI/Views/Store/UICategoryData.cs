using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;

namespace CDPIUI;

public class UICategoryData
{
    public string StoreId { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }

    public ObservableCollection<UIElement> Items { get; set; }
}
