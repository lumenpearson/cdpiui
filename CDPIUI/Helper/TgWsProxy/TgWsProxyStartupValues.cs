using System.Collections.Generic;

namespace CDPIUI.Helper.TgWsProxy;

/// <summary>
/// The values the tg-ws-proxy settings page edits. <see cref="Options"/> holds the
/// string options by name (see <see cref="TgWsProxyStartupArguments"/>); an option that is
/// absent from the startup string is absent from the dictionary.
/// </summary>
internal sealed class TgWsProxyStartupValues
{
    public Dictionary<string, string> Options { get; } = [];

    public bool Verbose { get; set; }

    public List<TgWsProxyDataCenter> DataCenters { get; } = [];
}
