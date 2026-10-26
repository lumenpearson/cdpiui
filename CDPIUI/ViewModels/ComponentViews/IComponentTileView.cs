using System.Threading.Tasks;

namespace CDPIUI.ViewModels.ComponentViews;

/// <summary>
/// The windows a component tile opens for its view model.
/// </summary>
public interface IComponentTileView
{
    Task ShowOutputWindowAsync(string storeId);

    Task ShowComponentSettingsAsync(string storeId);
}
