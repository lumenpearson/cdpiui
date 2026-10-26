using System;
using System.Threading.Tasks;

namespace CDPIUI.Helper;

public static class UrlOpenHelper
{
    public const string MainRepoUrl = "https://github.com/Storik4pro/cdpiui";
    public const string LicenseUrl = "https://github.com/Storik4pro/cdpiui/blob/main/LICENSE.txt";
    public const string ReportUrl = "https://github.com/Storik4pro/cdpiui/issues";

    public const string TelegramMainUrl = "https://t.me/storik4dev";
    public const string TelegramMemeUrl = "https://t.me/storik4m";
    public const string TelegramLUrl = "https://t.me/storik4l";

    public const string DonateUrl = "https://pay.cloudtips.ru/p/5bb7ff74";


    public const string NTCParty = "https://ntc.party";
    public const string BBD = "https://t.me/byebyedpi_group";

    // The Launch* methods catch and log their own errors, so callers can discard the task.

    /// <summary>
    /// Opens a web link. Links come from store manifests and license data, so file:, ms-*
    /// and other schemes that start local programs are refused.
    /// </summary>
    public static async Task LaunchUrl(string uri)
    {
        try
        {
            if (!TryGetWebUri(uri, out Uri webUri))
            {
                Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), $"Refused to open \"{uri}\"");
                return;
            }
            _ = await Windows.System.Launcher.LaunchUriAsync(webUri);
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }

    public static bool TryGetWebUri(string uri, out Uri webUri)
    {
        return Uri.TryCreate(uri?.Trim(), UriKind.Absolute, out webUri) &&
            (webUri.Scheme == Uri.UriSchemeHttps || webUri.Scheme == Uri.UriSchemeHttp ||
             webUri.Scheme == Uri.UriSchemeMailto || webUri.Scheme == "tg");
    }
    public static async Task LaunchReportUrl()
    {
        try
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(ReportUrl));
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }
    public static async Task LaunchMainRepoUrl()
    {
        try
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(MainRepoUrl));
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }
    public static async Task LaunchWikiUrl()
    {
        try
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://storik4pro.github.io/cdpiui/"));
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }
    public static async Task LaunchLicenseUrl()
    {
        try
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(LicenseUrl));
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }
    public static async Task LaunchDonateUrl()
    {
        try
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(
                new Uri(DonateUrl));
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }
    public static async Task LaunchTelegramUrl()
    {
        try
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(TelegramMainUrl));
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }

    public static async Task LaunchTelegramProxyUrl(string ip, string port, string secret)
    {
        try
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(
                new Uri($"tg://proxy?server={ip}&port={port}&secret={secret}"));
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }
    public static async Task LaunchComponentForumUrl(string componentId)
    {
        try
        {
            switch (componentId)
            {
                case "CSZTBN012":
                    _ = await Windows.System.Launcher.LaunchUriAsync(
                        new Uri("https://ntc.party/c/community-software/zapret-antidpi/20"));
                    break;
                case "CSGIVS036":
                    _ = await Windows.System.Launcher.LaunchUriAsync(
                        new Uri("https://ntc.party/c/community-software/goodbyedpi/8"));
                    break;
                case "CSBIHA024":
                    _ = await Windows.System.Launcher.LaunchUriAsync(
                        new Uri("https://ntc.party/c/community-software/byedpi/39"));
                    break;
                case "CSSIXC048":
                    break;
                case "CSTYFL050":
                    _ = await Windows.System.Launcher.LaunchUriAsync(
                        new Uri("https://github.com/Flowseal/tg-ws-proxy/issues/389"));
                    break;

            }
        }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(UrlOpenHelper), exception.ToString());
        }
    }
}
