using System.Collections.Generic;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

// Retains the page's binding API; options own state, validation and settings independently.
public sealed partial class BlockCheck2ViewModel
{
    private readonly BlockCheck2OptionsViewModel _options;

    public string TargetInput
    {
        get => _options.TargetInput;
        set => _options.TargetInput = value;
    }

    public BlockCheckRunPreset SelectedRunPreset
    {
        get => _options.SelectedRunPreset;
        set => _options.SelectedRunPreset = value;
    }

    public bool IncludeHttp
    {
        get => _options.IncludeHttp;
        set => _options.IncludeHttp = value;
    }

    public bool IncludeTls12
    {
        get => _options.IncludeTls12;
        set => _options.IncludeTls12 = value;
    }

    public bool IncludeTls13
    {
        get => _options.IncludeTls13;
        set => _options.IncludeTls13 = value;
    }

    public bool IncludeTlsAuto
    {
        get => _options.IncludeTlsAuto;
        set => _options.IncludeTlsAuto = value;
    }

    public bool IncludeQuic
    {
        get => _options.IncludeQuic;
        set => _options.IncludeQuic = value;
    }

    public bool IncludeIpv4
    {
        get => _options.IncludeIpv4;
        set => _options.IncludeIpv4 = value;
    }

    public bool IncludeIpv6
    {
        get => _options.IncludeIpv6;
        set => _options.IncludeIpv6 = value;
    }

    public double ConnectTimeoutSeconds
    {
        get => _options.ConnectTimeoutSeconds;
        set => _options.ConnectTimeoutSeconds = value;
    }

    public double RequestTimeoutSeconds
    {
        get => _options.RequestTimeoutSeconds;
        set => _options.RequestTimeoutSeconds = value;
    }

    public double StartupGraceSeconds
    {
        get => _options.StartupGraceSeconds;
        set => _options.StartupGraceSeconds = value;
    }

    public bool TestAllStrategies
    {
        get => _options.TestAllStrategies;
        set => _options.TestAllStrategies = value;
    }

    public double RequestsPerDomain
    {
        get => _options.RequestsPerDomain;
        set => _options.RequestsPerDomain = value;
    }

    public IReadOnlyList<BlockCheckRunPreset> AvailableRunPresets => _options.AvailableRunPresets;
    public bool IsHttpOnly => _options.IsHttpOnly;
    public int DirectTargetInputCount => _options.DirectTargetInputCount;
    public bool HasTargetSources => _options.HasTargetSources;
    public bool HasProtocolSelection => _options.HasProtocolSelection;
    public bool HasIpVersionSelection => _options.HasIpVersionSelection;
    public bool CanContinueFromTargets => _options.CanContinueFromTargets;
    public bool AdvancedOptionsValid => _options.AdvancedOptionsValid;

    public void ResetAdvancedOptions() => _options.ResetAdvancedOptions();

    private BlockCheckSessionRequest CreateSessionRequest() => _options.CreateSessionRequest(
        () => SelectedSiteLists.Select(item => item.CreateInput(_generatedSiteListRoot)).ToArray());
}
