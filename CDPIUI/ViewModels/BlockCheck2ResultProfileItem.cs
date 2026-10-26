using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2ResultProfileItem
{
    public BlockCheck2ResultProfileItem(BlockCheckReportProfile profile)
    {
        Name = profile.Name;
        Route = $"{profile.Layer7Protocol}/{profile.Transport}/{profile.IpVersion}:{profile.Port}";
        Domains = string.Join(", ", profile.Domains);
        SiteLists = string.Join(", ", profile.HostListPaths);
        Sources = string.Join(", ", profile.HostListPaths.Concat(profile.Domains));
        PrimaryStrategy = profile.PrimaryStrategyId;
        FallbackStrategies = string.Join(", ", profile.FallbackStrategyIds);
        TargetCount = profile.TargetIds.Count;
        IsBestEffort = profile.IsBestEffort;
    }

    public string Name { get; }
    public string Route { get; }
    public string Domains { get; }
    public string SiteLists { get; }
    public string Sources { get; }
    public string PrimaryStrategy { get; }
    public string FallbackStrategies { get; }
    public int TargetCount { get; }
    public bool IsBestEffort { get; }
}
