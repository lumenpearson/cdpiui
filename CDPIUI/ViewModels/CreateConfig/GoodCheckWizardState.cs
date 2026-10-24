namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>The steps of the wizard that creates a config with GoodCheck, in order.</summary>
public enum GoodCheckWizardState
{
    None,
    LetsBegin,
    PrepareLoading,
    SiteListsSettings,
    StrategyListSettings,
    AdditionalSettings,
}
