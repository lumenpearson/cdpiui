using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.AddOns.GoodCheck;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>
/// What <see cref="CreateViaGoodCheckViewModel"/> needs from the page: the step panels and
/// their animation, the strategy choosers, dialogs and navigation.
/// </summary>
public interface ICreateViaGoodCheckView
{
    void HideAllPanels();

    /// <summary>
    /// Slides from the panel of one step to the panel of another. Returns
    /// <see langword="false"/> when the new step has no panel.
    /// </summary>
    bool SwitchPanel(GoodCheckWizardState oldState, GoodCheckWizardState newState, bool slideToLeft);

    void ClearStrategyChoosers();

    /// <summary>Adds the control that picks the strategy list for one site list.</summary>
    void AddStrategyChooser(SiteListElement siteList, List<GoodCheckStrategiesList> strategiesLists);

    /// <summary>Shows the error and closes the wizard window.</summary>
    Task ShowErrorAsync(string message);

    /// <summary>Leaves the wizard from its first step.</summary>
    void LeaveWizard();

    void OpenWorkPage();
}
