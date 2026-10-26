using static CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters.Zapret1OptionValues;
using static CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters.Zapret2ArgumentFormatter;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// Builds the arguments shared by the actions of a profile (TTL, TCP flags, IP id, fooling and
/// repeats), their packet ranges, and the action of the Zapret1 --dup options.
/// </summary>
internal sealed class Zapret1ActionArgsBuilder
{
    private readonly ZapretConversionContext _context;

    public Zapret1ActionArgsBuilder(ZapretConversionContext context)
    {
        _context = context;
    }

    public void AppendDuplicateAction(
        List<string> output,
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        string? repeatValue = LastValue(legacy, "dup");
        if (string.IsNullOrWhiteSpace(repeatValue))
        {
            return;
        }

        string range = BuildRange(
            LastValue(legacy, "dup-start"),
            LastValue(legacy, "dup-cutoff"),
            profileIndex,
            "dup");

        List<string> args = BuildCommonActionArgs(legacy, "dup", profileIndex);
        args.Add("dir=out");
        args.Add($"repeats={repeatValue}");

        output.Add("--payload=all");
        output.Add(FormatOption("out-range", range));
        output.Add(FormatLuaDesync("send", args));

        if (IsEnabled(legacy, "dup-replace"))
        {
            output.Add("--lua-desync=drop:dir=out");
        }
    }

    public List<string> BuildCommonActionArgs(
        Dictionary<string, List<string?>> legacy,
        string prefix,
        int profileIndex)
    {
        List<string> args = [];

        string optionPrefix = prefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? "dup"
            : "dpi-desync";

        string? ttl = LastValue(legacy, $"{optionPrefix}-ttl");
        string? ttl6 = LastValue(legacy, $"{optionPrefix}-ttl6") ?? ttl;
        AddValueArg(args, "ip_ttl", ttl);
        AddValueArg(args, "ip6_ttl", ttl6);

        AppendAutoTtlArgs(args, legacy, optionPrefix);

        string? setFlags = LastValue(legacy, $"{optionPrefix}-tcp-flags-set");
        string? unsetFlags = LastValue(legacy, $"{optionPrefix}-tcp-flags-unset");
        AddValueArg(args, "tcp_flags_set", NormalizeTcpFlags(setFlags));
        AddValueArg(args, "tcp_flags_unset", NormalizeTcpFlags(unsetFlags));

        string? ipId = prefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? LastValue(legacy, "dup-ip-id") ?? LastValue(legacy, "ip-id")
            : LastValue(legacy, "ip-id");
        AppendIpIdArgs(args, ipId);

        string? fooling = LastValue(legacy, $"{optionPrefix}-fooling");
        AppendFoolingArgs(args, fooling, legacy, optionPrefix, profileIndex);

        if (!prefix.Equals("dup", StringComparison.OrdinalIgnoreCase))
        {
            AddValueArg(args, "repeats", LastValue(legacy, "dpi-desync-repeats"));
        }

        return args;
    }

    private static void AppendAutoTtlArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        string optionPrefix)
    {
        string defaultAutoTtl = optionPrefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? "+1:3-64"
            : "1:3-20";
        string? autoTtl = OptionalValue(legacy, $"{optionPrefix}-autottl", defaultAutoTtl);
        string? autoTtl6 = legacy.ContainsKey($"{optionPrefix}-autottl6")
            ? OptionalValue(legacy, $"{optionPrefix}-autottl6", autoTtl ?? defaultAutoTtl)
            : autoTtl;
        string defaultAutoTtlRange = optionPrefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? "3-64"
            : "3-20";
        AddAutoTtlArg(args, "ip_autottl", autoTtl, defaultAutoTtlRange);
        AddAutoTtlArg(args, "ip6_autottl", autoTtl6, defaultAutoTtlRange);
    }

    private static void AppendIpIdArgs(List<string> args, string? ipId)
    {
        if (string.IsNullOrWhiteSpace(ipId))
        {
            return;
        }

        if (ipId.Equals("same", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("ip_id=none");
        }
        else if (ipId.Equals("seqgroup", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("ip_id=seq");
            args.Add("ip_id_conn");
        }
        else
        {
            args.Add($"ip_id={ipId}");
        }
    }

    private void AppendFoolingArgs(
        List<string> args,
        string? fooling,
        Dictionary<string, List<string?>> legacy,
        string optionPrefix,
        int profileIndex)
    {
        if (string.IsNullOrWhiteSpace(fooling))
        {
            return;
        }

        foreach (string mode in SplitCsv(fooling))
        {
            switch (mode.ToLowerInvariant())
            {
                case "none":
                    break;
                case "md5sig":
                    args.Add("tcp_md5");
                    break;
                case "badsum":
                    args.Add("badsum");
                    break;
                case "datanoack":
                    args.Add("tcp_flags_unset=ack");
                    break;
                case "ts":
                    args.Add($"tcp_ts={LastValue(legacy, $"{optionPrefix}-ts-increment") ?? "-600000"}");
                    break;
                case "badseq":
                    AppendBadSeqArgs(args, legacy, optionPrefix);
                    break;
                case "hopbyhop":
                    args.Add("ip6_hopbyhop");
                    break;
                case "hopbyhop2":
                    args.Add("ip6_hopbyhop");
                    args.Add("ip6_hopbyhop2");
                    break;
                default:
                    _context.AddError(
                        "UNSUPPORTED_FOOLING",
                        $"Zapret1 fooling mode '{mode}' has no conversion rule.",
                        $"{optionPrefix}-fooling",
                        profileIndex);
                    break;
            }
        }
    }

    private static void AppendBadSeqArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        string optionPrefix)
    {
        string sequence = LastValue(legacy, $"{optionPrefix}-badseq-increment") ?? "-10000";
        if (!IsZero(sequence))
        {
            args.Add($"tcp_seq={sequence}");
        }
        else
        {
            args.Add($"tcp_ack={LastValue(legacy, $"{optionPrefix}-badack-increment") ?? "-66000"}");
            args.Add("tcp_ts_up");
        }
    }

    public string BuildRange(
        string? start,
        string? cutoff,
        int profileIndex,
        string optionPrefix)
    {
        string? normalizedStart = NormalizeRangeCounter(start);
        string? normalizedCutoff = NormalizeRangeCounter(cutoff);

        if (start != null && normalizedStart == null)
        {
            _context.AddError(
                "INVALID_RANGE",
                $"Invalid Zapret1 range value '{start}'.",
                $"{optionPrefix}-start",
                profileIndex);
            return "a";
        }
        if (cutoff != null && normalizedCutoff == null)
        {
            _context.AddError(
                "INVALID_RANGE",
                $"Invalid Zapret1 range value '{cutoff}'.",
                $"{optionPrefix}-cutoff",
                profileIndex);
            return "a";
        }

        if (normalizedStart != null && normalizedCutoff != null)
        {
            return $"{normalizedStart}<{normalizedCutoff}";
        }
        if (normalizedStart != null)
        {
            return $"{normalizedStart}-";
        }
        if (normalizedCutoff != null)
        {
            return $"<{normalizedCutoff}";
        }
        return "a";
    }
}
