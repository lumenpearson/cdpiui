using CDPIUI.Core.Store.Data;

namespace CDPIUI.Core.Proxy;

public enum ProxySetupTypes
{
    None,
    AllSystem,
    ProxiFyre,
    NoActions,
    AsInConfig,
}

public static class ProxyHelper
{
    public static List<string> Tokens = new() { "-p", "--port", "-i", "--ip", "-addr", "-port", "--host" };

    /// <summary>
    /// Removes the address and port arguments, which the application sets itself
    /// </summary>
    public static string ReplaceIp(string args)
    {
        List<string> tokens = SplitArguments(args);
        string finalArgs = string.Empty;
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            int separator = token.IndexOf('=');
            string name = separator >= 0 ? token[..separator] : token;
            if (Tokens.Contains(name))
            {
                // "--port 8080" takes the next token as its value, "--port=8080" does not.
                if (separator < 0)
                    i++;
                continue;
            }

            // The token is kept whole: "--x=a=b" and quoted paths with spaces stay intact.
            finalArgs += $"{token} ";
        }
        return finalArgs;
    }

    // Whitespace separates arguments except inside double quotes, as on the command line.
    private static List<string> SplitArguments(string args)
    {
        var tokens = new List<string>();
        var current = new global::System.Text.StringBuilder();
        bool quoted = false;
        foreach (char c in args ?? string.Empty)
        {
            if (c == '"')
                quoted = !quoted;

            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }

    /// <summary>
    /// Whether a proxy component listening on <paramref name="ip"/> is reachable only from this computer.
    /// Any other address, including 0.0.0.0, opens the proxy to the network without authentication.
    /// </summary>
    public static bool IsLoopbackAddress(string? ip)
    {
        string value = (ip ?? "").Trim().Trim('[', ']');
        if (string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        return global::System.Net.IPAddress.TryParse(value, out var address)
            && global::System.Net.IPAddress.IsLoopback(address);
    }

    /// <summary>
    /// Whether <paramref name="port"/> is a TCP port number. Any text was accepted and
    /// written into the system proxy settings and app-config.json.
    /// </summary>
    public static bool IsValidPort(string? port) =>
        int.TryParse((port ?? string.Empty).Trim(), global::System.Globalization.NumberStyles.None,
            global::System.Globalization.CultureInfo.InvariantCulture, out int value) &&
        value is >= 1 and <= 65535;

    public static List<string> ProxyLikeComponents = [
        HardcodedItemIds.ComponentIds[Components.ByeDPI],
        HardcodedItemIds.ComponentIds[Components.NoDPI],
        HardcodedItemIds.ComponentIds[Components.SpoofDPI]
            ];

    internal static string ReplaceArgsForProxy(
        string args,
        string? ip,
        string? port,
        string componentId)
    {
        string finalArgs = ReplaceIp(args);
        if (componentId == "CSSIXC048")
            finalArgs = $"-addr={ip} -port={port} " + finalArgs;
        else if (componentId == "CSNIG9025")
            finalArgs = $"--host={ip} --port={port} " + finalArgs;
        else
            finalArgs = $"--ip={ip} --port={port} " + finalArgs;
        return finalArgs;

    }
}
