using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.BlockCheck2;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUI3Localizer;
using TextDecorations = Windows.UI.Text.TextDecorations;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class MainPage
{
    private void UpdateReviewSummary(BlockCheck2PreparationPreview preview)
    {
        ReviewSourcesTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewSourcesFormat"),
            ViewModel.SelectedSiteListCount,
            ViewModel.SelectedDomainCount,
            preview.TargetCount);

        string[] protocols =
        [
            .. new[]
            {
                ViewModel.IncludeHttp ? "HTTP" : null,
                ViewModel.IncludeTls12 ? "TLS 1.2" : null,
                ViewModel.IncludeTls13 ? "TLS 1.3" : null,
                ViewModel.IncludeTlsAuto ? Text("BlockCheck2TlsAutoShortName") : null,
                ViewModel.IncludeQuic ? "QUIC / HTTP/3" : null,
            }.OfType<string>(),
        ];
        string[] ipVersions =
        [
            .. new[]
            {
                ViewModel.IncludeIpv4 ? "IPv4" : null,
                ViewModel.IncludeIpv6 ? "IPv6" : null,
            }.OfType<string>(),
        ];
        ReviewConnectionsTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewConnectionsFormat"),
            string.Join(", ", protocols),
            string.Join(", ", ipVersions));
        string modeName = ViewModel.SelectedRunPreset switch
        {
            BlockCheckRunPreset.Quick => Text("BlockCheck2QuickModeName"),
            BlockCheckRunPreset.Exhaustive => Text("BlockCheck2ExhaustiveModeName"),
            _ => Text("BlockCheck2BalancedModeName"),
        };
        bool testsAllStrategies = ViewModel.TestAllStrategies ||
                                  ViewModel.SelectedRunPreset == BlockCheckRunPreset.Exhaustive;
        ReviewModeTextBlock.Text = testsAllStrategies
            ? $"{modeName}. {Text("BlockCheck2ReviewAllStrategies")}"
            : modeName;
        ReviewRequestsTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewRequestsFormat"),
            ViewModel.RequestsPerDomain);
        ReviewTimeoutsTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewTimeoutsFormat"),
            ViewModel.ConnectTimeoutSeconds,
            ViewModel.RequestTimeoutSeconds,
            ViewModel.StartupGraceSeconds);
        ReviewMaximumTimeTextBlock.Text = preview.MaximumDuration == null
            ? Text("BlockCheck2ReviewMaximumTimeUnavailable")
            : string.Format(
                Text("BlockCheck2ReviewMaximumTimeFormat"),
                _durationText.FormatDuration(preview.MaximumDuration.Duration),
                preview.MaximumDuration.EstimatedStrategyJobs,
                preview.MaximumDuration.EstimatedRequests);
    }
}
