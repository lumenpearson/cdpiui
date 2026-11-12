namespace CDPIUI.ViewModels.BlockCheck2;

internal interface IBlockCheck2NavigationSource
{
    bool IsRunning { get; }
    bool CanCancel { get; }
    bool HasTargetSources { get; }
    bool AdvancedOptionsValid { get; }
    bool CanContinueFromTargets { get; }
    bool CanStart { get; }
    bool HasReport { get; }
}
