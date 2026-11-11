namespace CDPIUI.ViewModels.BlockCheck2;

// A live navigation view of the same Session; construction reads no values.
internal sealed class BlockCheck2NavigationSource : IBlockCheck2NavigationSource
{
    private readonly BlockCheck2ViewModel _session;

    public BlockCheck2NavigationSource(BlockCheck2ViewModel session) => _session = session;

    public bool IsRunning => _session.IsRunning;
    public bool CanCancel => _session.CanCancel;
    public bool HasTargetSources => _session.HasTargetSources;
    public bool AdvancedOptionsValid => _session.AdvancedOptionsValid;
    public bool CanContinueFromTargets => _session.CanContinueFromTargets;
    public bool CanStart => _session.CanStart;
    public bool HasReport => _session.LastReport != null;
}
