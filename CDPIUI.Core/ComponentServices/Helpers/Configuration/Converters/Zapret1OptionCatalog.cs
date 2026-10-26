namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// The Zapret1 options the converter knows, grouped by how they are converted.
/// </summary>
internal static class Zapret1OptionCatalog
{
    public static readonly HashSet<string> GlobalOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "debug", "dry-run", "version", "comment", "intercept", "ctrack-timeouts",
        "ctrack-disable", "ipcache-lifetime", "ipcache-hostname", "wf-iface", "wf-l3",
        "wf-tcp", "wf-udp", "wf-tcp-in", "wf-tcp-out", "wf-udp-in", "wf-udp-out",
        "wf-tcp-empty", "wf-icmp-in", "wf-icmp-out", "wf-ipp-in", "wf-ipp-out",
        "wf-raw-part", "wf-raw-filter", "wf-filter-lan", "wf-raw", "wf-dup-check",
        "wf-save", "ssid-filter", "nlm-filter"
    };

    public static readonly HashSet<string> ProfileOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "filter-l3", "filter-tcp", "filter-udp", "filter-icmp", "filter-ipp", "filter-l7",
        "ipset", "ipset-ip", "ipset-exclude", "ipset-exclude-ip", "hostlist",
        "hostlist-domains", "hostlist-exclude", "hostlist-exclude-domains", "hostlist-auto",
        "hostlist-auto-fail-threshold", "hostlist-auto-fail-time",
        "hostlist-auto-retrans-threshold", "hostlist-auto-retrans-reset",
        "hostlist-auto-retrans-maxseq", "hostlist-auto-incoming-maxseq",
        "hostlist-auto-udp-out", "hostlist-auto-udp-in", "hostlist-auto-debug",
        "filter-ssid", "name", "skip"
    };

    public static readonly HashSet<string> ProfileInputFileOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ipset", "ipset-exclude", "hostlist", "hostlist-exclude"
    };

    public static readonly HashSet<string> SupportedLegacyOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ip-id",
        "dup", "dup-replace", "dup-ttl", "dup-ttl6", "dup-autottl", "dup-autottl6",
        "dup-tcp-flags-set", "dup-tcp-flags-unset", "dup-fooling", "dup-ts-increment",
        "dup-badseq-increment", "dup-badack-increment", "dup-ip-id", "dup-start", "dup-cutoff",
        "dpi-desync", "dpi-desync-ttl", "dpi-desync-ttl6", "dpi-desync-autottl",
        "dpi-desync-autottl6", "dpi-desync-tcp-flags-set", "dpi-desync-tcp-flags-unset",
        "dpi-desync-fooling", "dpi-desync-repeats", "dpi-desync-skip-nosni",
        "dpi-desync-split-pos", "dpi-desync-split-seqovl",
        "dpi-desync-split-seqovl-pattern", "dpi-desync-fakedsplit-pattern",
        "dpi-desync-fakedsplit-mod", "dpi-desync-hostfakesplit-midhost",
        "dpi-desync-hostfakesplit-mod", "dpi-desync-ipfrag-pos-tcp",
        "dpi-desync-ipfrag-pos-udp", "dpi-desync-ts-increment",
        "dpi-desync-badseq-increment", "dpi-desync-badack-increment",
        "dpi-desync-any-protocol", "dpi-desync-fake-http", "dpi-desync-fake-tls",
        "dpi-desync-fake-tls-mod", "dpi-desync-fake-unknown",
        "dpi-desync-fake-syndata", "dpi-desync-fake-quic",
        "dpi-desync-fake-wireguard", "dpi-desync-fake-dht",
        "dpi-desync-fake-discord", "dpi-desync-fake-stun",
        "dpi-desync-fake-unknown-udp", "dpi-desync-udplen-increment",
        "dpi-desync-udplen-pattern", "dpi-desync-start", "dpi-desync-cutoff"
    };

    // Names of older zapret1 versions, renamed in zapret 67 to the names Zapret2 also uses.
    public static readonly Dictionary<string, string> LegacyModeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["split2"] = "multisplit",
        ["disorder2"] = "multidisorder",
        ["split"] = "fakedsplit",
        ["disorder"] = "fakeddisorder",
    };

    // Options that only tune details of the packets and have no Zapret2 counterpart here. They are
    // dropped with a warning: failing the whole preset for them left the user with nothing.
    public static readonly HashSet<string> DroppedLegacyOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "wssize", "wssize-cutoff", "hostcase", "hostspell", "hostnospace", "domcase",
        "methodspace", "methodeol", "unixeol"
    };

    public static readonly HashSet<string> ZeroPhaseModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "synack", "syndata"
    };
}
