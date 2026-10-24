using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Core.Basic;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// Downloads the endpoint list of the online DPI checker suite.
/// </summary>
internal static class DpiSuiteSource
{
    private const string DpiSuiteUrl = "https://hyperion-cs.github.io/dpi-checkers/ru/tcp-16-20/suite.v2.json";
    // Optional override of DpiSuiteUrl in the settings file. An empty value keeps the default.
    private static readonly string[] DpiSuiteUrlSettingGroup = ["ADDONS", "PRESETTEST"];
    private const string DpiSuiteUrlSettingKey = "dpiSuiteUrl";

    public static async Task<List<DpiSuiteEntry>> FetchAsync(CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(CurlProcessRunner.TimeoutSeconds + 2) };
            string json = await client.GetStringAsync(GetDpiSuiteUrl(), ct);
            var entries = JsonSerializer.Deserialize<List<DpiSuiteEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return entries?.Where(e => e != null && !string.IsNullOrEmpty(e.host)).ToList();
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't fetch DPI suite: {ex.Message}");
            return null;
        }
    }

    private static string GetDpiSuiteUrl()
    {
        try
        {
            string custom = SettingsManager.Instance.GetValue<string>(DpiSuiteUrlSettingGroup, DpiSuiteUrlSettingKey);
            if (string.IsNullOrWhiteSpace(custom))
                return DpiSuiteUrl;

            custom = custom.Trim();
            if (Uri.TryCreate(custom, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeHttps)
                return uri.AbsoluteUri;

            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Ignored DPI suite URL {custom}: not an absolute https URL");
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't read DPI suite URL setting: {ex.Message}");
        }
        return DpiSuiteUrl;
    }
}
