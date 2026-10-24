using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Core.Store.ViewModels;

namespace CDPIUI.ViewModels.Welcome;

/// <summary>What <see cref="WelcomeViewModel"/> needs from the wizard window.</summary>
public interface IWelcomeView
{
    WelcomeStep CurrentStep { get; }

    /// <summary>Updates the navigation buttons for the current step.</summary>
    void RefreshNavigation();

    /// <summary>Shows only the Skip button while the Store kits load.</summary>
    void ShowStoreLoadingButtons();

    /// <summary>Asks the user to accept the licenses. Returns true when the user accepted.</summary>
    Task<bool> AcceptLicensesAsync(List<ItemLicenseModel> licenses);
}
