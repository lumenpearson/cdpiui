using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels.BlockCheck2;

internal interface IBlockCheck2ReviewSource
{
    int SelectedSiteListCount { get; }
    int SelectedDomainCount { get; }
    bool IncludeHttp { get; }
    bool IncludeTls12 { get; }
    bool IncludeTls13 { get; }
    bool IncludeTlsAuto { get; }
    bool IncludeQuic { get; }
    bool IncludeIpv4 { get; }
    bool IncludeIpv6 { get; }
    BlockCheckRunPreset SelectedRunPreset { get; }
    bool TestAllStrategies { get; }
    double RequestsPerDomain { get; }
    double ConnectTimeoutSeconds { get; }
    double RequestTimeoutSeconds { get; }
    double StartupGraceSeconds { get; }
}
