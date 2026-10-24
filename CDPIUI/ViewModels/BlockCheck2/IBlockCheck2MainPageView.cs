namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>The parts of the BlockCheck2 wizard page that only the page can do.</summary>
public interface IBlockCheck2MainPageView
{
    /// <summary>Shows the step with its animation.</summary>
    void GoToStep(BlockCheck2WizardStep step);

    /// <summary>Shows and hides the wizard buttons with their animation.</summary>
    void ShowNavigationButtons(BlockCheck2NavigationButtons buttons);
}
