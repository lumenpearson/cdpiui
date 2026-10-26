using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2MainPageViewModel
{
    private string _reviewSourcesText = string.Empty;
    private string _reviewConnectionsText = string.Empty;
    private string _reviewModeText = string.Empty;
    private string _reviewRequestsText = string.Empty;
    private string _reviewTimeoutsText = string.Empty;
    private string _reviewMaximumTimeText = string.Empty;

    public string ReviewSourcesText
    {
        get => _reviewSourcesText;
        private set => SetProperty(ref _reviewSourcesText, value);
    }

    public string ReviewConnectionsText
    {
        get => _reviewConnectionsText;
        private set => SetProperty(ref _reviewConnectionsText, value);
    }

    public string ReviewModeText
    {
        get => _reviewModeText;
        private set => SetProperty(ref _reviewModeText, value);
    }

    public string ReviewRequestsText
    {
        get => _reviewRequestsText;
        private set => SetProperty(ref _reviewRequestsText, value);
    }

    public string ReviewTimeoutsText
    {
        get => _reviewTimeoutsText;
        private set => SetProperty(ref _reviewTimeoutsText, value);
    }

    public string ReviewMaximumTimeText
    {
        get => _reviewMaximumTimeText;
        private set => SetProperty(ref _reviewMaximumTimeText, value);
    }

    private void UpdateReviewSummary(BlockCheck2PreparationPreview preview)
    {
        ReviewSourcesText = string.Format(
            Text("BlockCheck2ReviewSourcesFormat"),
            Session.SelectedSiteListCount,
            Session.SelectedDomainCount,
            preview.TargetCount);

        string[] protocols =
        [
            .. new[]
            {
                Session.IncludeHttp ? "HTTP" : null,
                Session.IncludeTls12 ? "TLS 1.2" : null,
                Session.IncludeTls13 ? "TLS 1.3" : null,
                Session.IncludeTlsAuto ? Text("BlockCheck2TlsAutoShortName") : null,
                Session.IncludeQuic ? "QUIC / HTTP/3" : null,
            }.OfType<string>(),
        ];
        string[] ipVersions =
        [
            .. new[]
            {
                Session.IncludeIpv4 ? "IPv4" : null,
                Session.IncludeIpv6 ? "IPv6" : null,
            }.OfType<string>(),
        ];
        ReviewConnectionsText = string.Format(
            Text("BlockCheck2ReviewConnectionsFormat"),
            string.Join(", ", protocols),
            string.Join(", ", ipVersions));
        string modeName = Session.SelectedRunPreset switch
        {
            BlockCheckRunPreset.Quick => Text("BlockCheck2QuickModeName"),
            BlockCheckRunPreset.Exhaustive => Text("BlockCheck2ExhaustiveModeName"),
            _ => Text("BlockCheck2BalancedModeName"),
        };
        bool testsAllStrategies = Session.TestAllStrategies ||
                                  Session.SelectedRunPreset == BlockCheckRunPreset.Exhaustive;
        ReviewModeText = testsAllStrategies
            ? $"{modeName}. {Text("BlockCheck2ReviewAllStrategies")}"
            : modeName;
        ReviewRequestsText = string.Format(
            Text("BlockCheck2ReviewRequestsFormat"),
            Session.RequestsPerDomain);
        ReviewTimeoutsText = string.Format(
            Text("BlockCheck2ReviewTimeoutsFormat"),
            Session.ConnectTimeoutSeconds,
            Session.RequestTimeoutSeconds,
            Session.StartupGraceSeconds);
        ReviewMaximumTimeText = preview.MaximumDuration == null
            ? Text("BlockCheck2ReviewMaximumTimeUnavailable")
            : string.Format(
                Text("BlockCheck2ReviewMaximumTimeFormat"),
                _durationText.FormatDuration(preview.MaximumDuration.Duration),
                preview.MaximumDuration.EstimatedStrategyJobs,
                preview.MaximumDuration.EstimatedRequests);
    }
}
