#nullable enable

using System;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels.BlockCheck2;

internal sealed class BlockCheck2ReviewPresentation : ObservableObject
{
    private readonly IBlockCheck2ReviewSource _source;
    private readonly Func<string, string> _text;
    private readonly Func<TimeSpan, string> _formatDuration;
    private string _reviewSourcesText = string.Empty;
    private string _reviewConnectionsText = string.Empty;
    private string _reviewModeText = string.Empty;
    private string _reviewRequestsText = string.Empty;
    private string _reviewTimeoutsText = string.Empty;
    private string _reviewMaximumTimeText = string.Empty;

    public BlockCheck2ReviewPresentation(IBlockCheck2ReviewSource source,
        Func<string, string> text, Func<TimeSpan, string> formatDuration)
    {
        _source = source;
        _text = text;
        _formatDuration = formatDuration;
    }

    public string ReviewSourcesText
    {
        get => _reviewSourcesText;
        set => SetProperty(ref _reviewSourcesText, value);
    }

    public string ReviewConnectionsText
    {
        get => _reviewConnectionsText;
        set => SetProperty(ref _reviewConnectionsText, value);
    }

    public string ReviewModeText
    {
        get => _reviewModeText;
        set => SetProperty(ref _reviewModeText, value);
    }

    public string ReviewRequestsText
    {
        get => _reviewRequestsText;
        set => SetProperty(ref _reviewRequestsText, value);
    }

    public string ReviewTimeoutsText
    {
        get => _reviewTimeoutsText;
        set => SetProperty(ref _reviewTimeoutsText, value);
    }

    public string ReviewMaximumTimeText
    {
        get => _reviewMaximumTimeText;
        set => SetProperty(ref _reviewMaximumTimeText, value);
    }

    public void UpdateReviewSummary(BlockCheck2PreparationPreview preview)
    {
        ReviewSourcesText = string.Format(
            _text("BlockCheck2ReviewSourcesFormat"),
            _source.SelectedSiteListCount,
            _source.SelectedDomainCount,
            preview.TargetCount);

        string[] protocols =
        [
            .. new[]
            {
                _source.IncludeHttp ? "HTTP" : null,
                _source.IncludeTls12 ? "TLS 1.2" : null,
                _source.IncludeTls13 ? "TLS 1.3" : null,
                _source.IncludeTlsAuto ? _text("BlockCheck2TlsAutoShortName") : null,
                _source.IncludeQuic ? "QUIC / HTTP/3" : null,
            }.OfType<string>(),
        ];
        string[] ipVersions =
        [
            .. new[]
            {
                _source.IncludeIpv4 ? "IPv4" : null,
                _source.IncludeIpv6 ? "IPv6" : null,
            }.OfType<string>(),
        ];
        ReviewConnectionsText = string.Format(
            _text("BlockCheck2ReviewConnectionsFormat"),
            string.Join(", ", protocols),
            string.Join(", ", ipVersions));
        string modeName = _source.SelectedRunPreset switch
        {
            BlockCheckRunPreset.Quick => _text("BlockCheck2QuickModeName"),
            BlockCheckRunPreset.Exhaustive => _text("BlockCheck2ExhaustiveModeName"),
            _ => _text("BlockCheck2BalancedModeName"),
        };
        bool testsAllStrategies = _source.TestAllStrategies ||
                                  _source.SelectedRunPreset == BlockCheckRunPreset.Exhaustive;
        ReviewModeText = testsAllStrategies
            ? $"{modeName}. {_text("BlockCheck2ReviewAllStrategies")}"
            : modeName;
        ReviewRequestsText = string.Format(
            _text("BlockCheck2ReviewRequestsFormat"),
            _source.RequestsPerDomain);
        ReviewTimeoutsText = string.Format(
            _text("BlockCheck2ReviewTimeoutsFormat"),
            _source.ConnectTimeoutSeconds,
            _source.RequestTimeoutSeconds,
            _source.StartupGraceSeconds);
        ReviewMaximumTimeText = preview.MaximumDuration == null
            ? _text("BlockCheck2ReviewMaximumTimeUnavailable")
            : string.Format(
                _text("BlockCheck2ReviewMaximumTimeFormat"),
                _formatDuration(preview.MaximumDuration.Duration),
                preview.MaximumDuration.EstimatedStrategyJobs,
                preview.MaximumDuration.EstimatedRequests);
    }
}
