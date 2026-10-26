using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Core.Store.ViewModels;

namespace CDPIUI.ViewModels.Store;

/// <summary>What <see cref="ItemViewPageViewModel"/> needs from the item page.</summary>
public interface IItemViewPageView
{
    /// <summary>Shows the licenses; returns <see langword="true"/> when the user accepts them.</summary>
    Task<bool> AcceptLicensesAsync(List<ItemLicenseModel> licenses);

    /// <summary>Asks whether to reinstall the item.</summary>
    Task<bool> ConfirmReinstallAsync();

    /// <summary>Opens the settings of an installed component in the main window.</summary>
    Task OpenComponentSettingsAsync(string componentId);

    /// <summary>Creates the buttons of the external links.</summary>
    void ShowLinks(List<Link> links);
}
