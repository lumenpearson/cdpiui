using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels.BlockCheck2;

// A live view of Session. No option value is copied when this adapter is created.
internal sealed class BlockCheck2ReviewSource : IBlockCheck2ReviewSource
{
    private readonly BlockCheck2ViewModel _session;

    public BlockCheck2ReviewSource(BlockCheck2ViewModel session) => _session = session;

    public int SelectedSiteListCount => _session.SelectedSiteListCount;
    public int SelectedDomainCount => _session.SelectedDomainCount;
    public bool IncludeHttp => _session.IncludeHttp;
    public bool IncludeTls12 => _session.IncludeTls12;
    public bool IncludeTls13 => _session.IncludeTls13;
    public bool IncludeTlsAuto => _session.IncludeTlsAuto;
    public bool IncludeQuic => _session.IncludeQuic;
    public bool IncludeIpv4 => _session.IncludeIpv4;
    public bool IncludeIpv6 => _session.IncludeIpv6;
    public BlockCheckRunPreset SelectedRunPreset => _session.SelectedRunPreset;
    public bool TestAllStrategies => _session.TestAllStrategies;
    public double RequestsPerDomain => _session.RequestsPerDomain;
    public double ConnectTimeoutSeconds => _session.ConnectTimeoutSeconds;
    public double RequestTimeoutSeconds => _session.RequestTimeoutSeconds;
    public double StartupGraceSeconds => _session.StartupGraceSeconds;
}
