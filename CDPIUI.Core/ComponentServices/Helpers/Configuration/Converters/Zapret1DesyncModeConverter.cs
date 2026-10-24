using static CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters.Zapret1OptionValues;
using static CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters.Zapret2ArgumentFormatter;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// Converts one Zapret1 --dpi-desync mode of a profile to Zapret2 --lua-desync actions.
/// </summary>
internal sealed class Zapret1DesyncModeConverter
{
    private readonly ZapretConversionContext _context;

    public Zapret1DesyncModeConverter(ZapretConversionContext context)
    {
        _context = context;
    }

    public void AppendMode(
        List<string> output,
        string mode,
        Dictionary<string, List<string?>> legacy,
        List<string> commonArgs,
        bool anyProtocol,
        bool skipNoSni,
        int profileIndex)
    {
        string normalizedMode = mode.ToLowerInvariant();
        List<string> args = BuildModeArgs(normalizedMode, commonArgs, anyProtocol);

        switch (normalizedMode)
        {
            case "fake":
            case "fakeknown":
                output.Add(ConvertFake(args, legacy, skipNoSni, profileIndex));
                break;

            case "multisplit":
            case "multidisorder":
                output.Add(ConvertMultiSplit(normalizedMode, args, legacy, skipNoSni, profileIndex));
                break;

            case "fakedsplit":
            case "fakeddisorder":
                AppendSplitArgs(args, legacy, normalizedMode, profileIndex);
                AppendFakeSplitArgs(args, legacy, profileIndex);
                output.Add(FormatLegacyLuaDesync(normalizedMode, args, skipNoSni));
                break;

            case "hostfakesplit":
                AppendHostFakeSplitArgs(args, legacy, profileIndex);
                output.Add(FormatLegacyLuaDesync("hostfakesplit", args, skipNoSni));
                break;

            case "syndata":
                output.Add(ConvertSynData(args, legacy, skipNoSni, profileIndex));
                break;

            case "synack":
                output.Add(FormatLegacyLuaDesync("synack", args, skipNoSni));
                break;

            case "rst":
                output.Add(FormatLegacyLuaDesync("rst", args, skipNoSni));
                break;

            case "rstack":
                args.Add("rstack");
                output.Add(FormatLegacyLuaDesync("rst", args, skipNoSni));
                break;

            case "ipfrag2":
                args.Add("ipfrag");
                AddValueArg(args, "ipfrag_pos_tcp", LastValue(legacy, "dpi-desync-ipfrag-pos-tcp"));
                AddValueArg(args, "ipfrag_pos_udp", LastValue(legacy, "dpi-desync-ipfrag-pos-udp"));
                output.Add(FormatLegacyLuaDesync("send", args, skipNoSni));
                output.Add(FormatLegacyLuaDesync("drop", [], skipNoSni));
                break;

            case "udplen":
                output.Add(ConvertUdpLength(args, legacy, skipNoSni, profileIndex));
                break;

            case "tamper":
                output.Add(FormatLegacyLuaDesync("dht_dn", args, skipNoSni));
                break;

            default:
                _context.AddError(
                    "UNSUPPORTED_STRATEGY",
                    $"Zapret1 strategy '{mode}' has no Zapret2 conversion rule.",
                    "dpi-desync",
                    profileIndex);
                break;
        }
    }

    private static List<string> BuildModeArgs(string normalizedMode, List<string> commonArgs, bool anyProtocol)
    {
        bool sendsOriginalSplitParts = normalizedMode is "multisplit" or "multidisorder";
        List<string> args = sendsOriginalSplitParts
            ? commonArgs.Where(arg =>
                arg.Equals("ip_id_conn", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("ip_id=", StringComparison.OrdinalIgnoreCase)).ToList()
            : [.. commonArgs];

        if (anyProtocol &&
            !normalizedMode.Equals("fakeknown", StringComparison.OrdinalIgnoreCase) &&
            !Zapret1OptionCatalog.ZeroPhaseModes.Contains(normalizedMode))
        {
            args.Add("payload=~empty");
        }

        return args;
    }

    private string ConvertFake(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        bool skipNoSni,
        int profileIndex)
    {
        args.InsertRange(0, BuildFakePayloadArgs(legacy, profileIndex));
        string? tlsMod = LastValue(legacy, "dpi-desync-fake-tls-mod");
        if (!string.IsNullOrWhiteSpace(tlsMod) &&
            !tlsMod.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            args.Add($"tls_mod={tlsMod}");
        }
        return FormatLegacyLuaDesync("fake", args, skipNoSni);
    }

    private string ConvertMultiSplit(
        string normalizedMode,
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        bool skipNoSni,
        int profileIndex)
    {
        AppendSplitArgs(args, legacy, normalizedMode, profileIndex);
        RemoveArg(args, "repeats");

        string function = normalizedMode == "multidisorder" && _context.Options.PreferLegacyMultidisorder
            ? "multidisorder_legacy"
            : normalizedMode;
        return FormatLegacyLuaDesync(function, args, skipNoSni);
    }

    private string ConvertSynData(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        bool skipNoSni,
        int profileIndex)
    {
        string? syndata = LastValue(legacy, "dpi-desync-fake-syndata");
        if (!string.IsNullOrWhiteSpace(syndata))
        {
            args.Insert(0, $"blob={_context.GetBlobValue(syndata, profileIndex, "dpi-desync-fake-syndata")}");
        }
        return FormatLegacyLuaDesync("syndata", args, skipNoSni);
    }

    private string ConvertUdpLength(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        bool skipNoSni,
        int profileIndex)
    {
        AddValueArg(args, "increment", LastValue(legacy, "dpi-desync-udplen-increment"));
        string? pattern = LastValue(legacy, "dpi-desync-udplen-pattern");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            args.Add($"pattern={_context.GetBlobValue(pattern, profileIndex, "dpi-desync-udplen-pattern")}");
        }
        return FormatLegacyLuaDesync("udplen", args, skipNoSni);
    }

    private List<string> BuildFakePayloadArgs(
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        const string zero64 = "cdpi_z1_zero64";
        const string zero256 = "cdpi_z1_zero256";

        string Blob(string optionName, string defaultValue) =>
            _context.GetBlobValueOrDefault(LastValue(legacy, optionName), defaultValue, profileIndex, optionName);

        string http = Blob("dpi-desync-fake-http", "fake_default_http");
        string tls = Blob("dpi-desync-fake-tls", "fake_default_tls");
        string quic = Blob("dpi-desync-fake-quic", "fake_default_quic");
        string wireguard = Blob("dpi-desync-fake-wireguard", zero64);
        string dht = Blob("dpi-desync-fake-dht", zero64);
        string discord = Blob("dpi-desync-fake-discord", zero64);
        string stun = Blob("dpi-desync-fake-stun", zero64);
        string unknown = Blob("dpi-desync-fake-unknown", zero256);
        string unknownUdp = Blob("dpi-desync-fake-unknown-udp", zero64);

        return
        [
            $"http_req={http}",
            $"tls_client_hello={tls}",
            $"quic_initial={quic}",
            $"wireguard_initiation={wireguard}",
            $"wireguard_response={wireguard}",
            $"wireguard_cookie={wireguard}",
            $"wireguard_keepalive={wireguard}",
            $"dht={dht}",
            $"utp_bt_handshake={zero64}",
            $"discord_ip_discovery={discord}",
            $"stun={stun}",
            $"unknown={unknown}",
            $"unknown_udp={unknownUdp}"
        ];
    }

    private void AppendSplitArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        string mode,
        int profileIndex)
    {
        AddValueArg(args, "pos", LastValue(legacy, "dpi-desync-split-pos"));
        AddValueArg(args, "seqovl", LastValue(legacy, "dpi-desync-split-seqovl"));

        string? pattern = LastValue(legacy, "dpi-desync-split-seqovl-pattern");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            args.Add($"seqovl_pattern={_context.GetBlobValue(pattern, profileIndex, "dpi-desync-split-seqovl-pattern")}");
        }

        if (mode.Equals("multisplit", StringComparison.OrdinalIgnoreCase))
        {
            string? seqovl = LastValue(legacy, "dpi-desync-split-seqovl");
            if (!string.IsNullOrWhiteSpace(seqovl) && !int.TryParse(seqovl, out _))
            {
                _context.AddError(
                    "MULTISPLIT_MARKER_SEQOVL",
                    "Zapret2 multisplit accepts only a numeric seqovl value.",
                    "dpi-desync-split-seqovl",
                    profileIndex);
            }
        }
    }

    private void AppendFakeSplitArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        string? pattern = LastValue(legacy, "dpi-desync-fakedsplit-pattern");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            args.Add($"pattern={_context.GetBlobValue(pattern, profileIndex, "dpi-desync-fakedsplit-pattern")}");
        }

        string? modifiers = LastValue(legacy, "dpi-desync-fakedsplit-mod");
        if (string.IsNullOrWhiteSpace(modifiers) || modifiers.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (string modifier in SplitCsv(modifiers))
        {
            if (!modifier.StartsWith("altorder=", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(modifier["altorder=".Length..], out int altOrder))
            {
                _context.AddError(
                    "UNSUPPORTED_FAKEDSPLIT_MODIFIER",
                    $"Unsupported fakedsplit modifier '{modifier}'.",
                    "dpi-desync-fakedsplit-mod",
                    profileIndex);
                continue;
            }

            AppendFakeSplitAltOrderArgs(args, altOrder, profileIndex);
        }
    }

    private void AppendFakeSplitAltOrderArgs(List<string> args, int altOrder, int profileIndex)
    {
        int packetMode = altOrder & 7;
        switch (packetMode)
        {
            case 0:
                break;
            case 1:
                args.Add("nofake1");
                break;
            case 2:
                args.Add("nofake1");
                args.Add("nofake2");
                break;
            case 3:
                args.Add("nofake1");
                args.Add("nofake2");
                args.Add("nofake4");
                break;
            default:
                _context.AddError(
                    "UNSUPPORTED_FAKEDSPLIT_ALTORDER",
                    $"Unsupported fakedsplit altorder '{altOrder}'.",
                    "dpi-desync-fakedsplit-mod",
                    profileIndex);
                break;
        }

        if ((altOrder & 24) != 0)
        {
            _context.AddWarning(
                "PARTIAL_FAKEDSPLIT_ALTORDER",
                $"The replay-only part of fakedsplit altorder '{altOrder}' has no exact Zapret2 equivalent.",
                "dpi-desync-fakedsplit-mod",
                profileIndex);
        }
    }

    private void AppendHostFakeSplitArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        AddValueArg(args, "midhost", LastValue(legacy, "dpi-desync-hostfakesplit-midhost"));

        string? modifiers = LastValue(legacy, "dpi-desync-hostfakesplit-mod");
        if (string.IsNullOrWhiteSpace(modifiers) || modifiers.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (string modifier in SplitCsv(modifiers))
        {
            if (modifier.StartsWith("host=", StringComparison.OrdinalIgnoreCase))
            {
                args.Add(modifier);
            }
            else if (modifier.Equals("altorder=1", StringComparison.OrdinalIgnoreCase))
            {
                _context.AddWarning(
                    "HOSTFAKESPLIT_ALTORDER_APPROXIMATION",
                    "hostfakesplit altorder=1 has no exact Zapret2 equivalent and was converted to the standard order.",
                    "dpi-desync-hostfakesplit-mod",
                    profileIndex);
            }
            else if (!modifier.Equals("altorder=0", StringComparison.OrdinalIgnoreCase))
            {
                _context.AddError(
                    "UNSUPPORTED_HOSTFAKESPLIT_MODIFIER",
                    $"Unsupported hostfakesplit modifier '{modifier}'.",
                    "dpi-desync-hostfakesplit-mod",
                    profileIndex);
            }
        }
    }
}
