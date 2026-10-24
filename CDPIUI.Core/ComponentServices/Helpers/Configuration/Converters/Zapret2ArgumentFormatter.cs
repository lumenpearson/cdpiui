namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// Writes Zapret2 options and the arguments of --lua-desync actions.
/// </summary>
internal static class Zapret2ArgumentFormatter
{
    private const string CompatibilityFunctionName = "cdpi_z1_desync";

    public const string CompatibilityLua =
        "cdpi_z1_zero64=string.rep(string.char(0),64); " +
        "cdpi_z1_zero256=string.rep(string.char(0),256); " +
        "cdpi_z1_funcs={fake=fake,multisplit=multisplit,multidisorder=multidisorder," +
        "multidisorder_legacy=multidisorder_legacy,fakedsplit=fakedsplit," +
        "fakeddisorder=fakeddisorder,hostfakesplit=hostfakesplit,syndata=syndata," +
        "synack=synack,rst=rst,send=send,drop=drop,udplen=udplen,dht_dn=dht_dn}; " +
        "function cdpi_z1_desync(ctx,d) " +
        "if d.arg.z1_skip_nosni and d.l7payload=='tls_client_hello' and " +
        "(not d.track or not d.track.hostname) then return end; " +
        "if d.arg.z1_func=='fake' then " +
        "local b=d.arg[d.l7payload]; " +
        "if not b then b=d.dis.udp and d.arg.unknown_udp or d.arg.unknown end; " +
        "if not b then return end; d.arg.blob=b end; " +
        "return cdpi_z1_funcs[d.arg.z1_func](ctx,d) " +
        "end";

    private static string NormalizeAutoTtl(string value, string defaultRange)
    {
        string normalized = value;
        if (normalized[0] is not ('+' or '-'))
        {
            normalized = "-" + normalized;
        }
        normalized = normalized.Replace(':', ',');
        if (!normalized.Contains(','))
        {
            normalized += $",{defaultRange}";
        }
        return normalized;
    }

    public static void AddAutoTtlArg(
        List<string> args,
        string name,
        string? value,
        string defaultRange)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "-" or "0:0-0" or "0,0-0")
        {
            return;
        }
        args.Add($"{name}={NormalizeAutoTtl(value, defaultRange)}");
    }

    public static void AddValueArg(List<string> args, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            args.Add($"{name}={value}");
        }
    }

    public static void RemoveArg(List<string> args, string name)
    {
        args.RemoveAll(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                              arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A --lua-desync action that calls a Zapret2 function through the compatibility wrapper.
    /// </summary>
    public static string FormatLegacyLuaDesync(
        string function,
        IEnumerable<string> args,
        bool skipNoSni)
    {
        List<string> compatibilityArgs = [$"z1_func={function}"];
        if (skipNoSni)
        {
            compatibilityArgs.Add("z1_skip_nosni=1");
        }
        compatibilityArgs.AddRange(args);
        return FormatLuaDesync(CompatibilityFunctionName, compatibilityArgs);
    }

    public static string FormatLuaDesync(string function, IEnumerable<string> args)
    {
        string[] values = args.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return values.Length == 0
            ? $"--lua-desync={function}"
            : $"--lua-desync={function}:{string.Join(':', values)}";
    }

    public static string FormatOption(string name, string? value)
    {
        if (value == null)
        {
            return $"--{name}";
        }

        bool mustQuote = value.Any(char.IsWhiteSpace) || value.Contains('"');
        string escaped = value.Replace("\"", "\\\"");
        return mustQuote
            ? $"--{name}=\"{escaped}\""
            : $"--{name}={escaped}";
    }
}
