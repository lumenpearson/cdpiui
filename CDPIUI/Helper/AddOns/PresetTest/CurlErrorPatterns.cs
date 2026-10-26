using System.Text.RegularExpressions;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// Patterns of the curl error output that the preset test classifies.
/// </summary>
internal static class CurlErrorPatterns
{
    // A failed DNS lookup is an ordinary error, not a certificate problem.
    public static readonly Regex Certificate = new(
        "certificate|SSL certificate problem|self[- ]?signed|certificate verify failed|unable to get local issuer certificate",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // curl on Windows uses schannel, and a TLS handshake reset by DPI ends with exit code 35
    // and "schannel: failed to receive handshake": that is a failure of the preset, not an
    // unsupported curl feature, so neither "schannel" nor code 35 alone means unsupported.
    public static readonly Regex Unsupported = new(
        "does not support|not supported|unsupported protocol|unsupported option|unsupported feature|Unrecognized option|Unknown option",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
