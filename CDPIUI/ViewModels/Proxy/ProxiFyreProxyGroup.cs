using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CDPIUI.ViewModels.Proxy;

/// <summary>A proxy group of the ProxiFyre app-config.json file.</summary>
public class ProxiFyreProxyGroup
{
    public List<string> appNames { get; set; }
    public string socks5ProxyEndpoint { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string username { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string password { get; set; }
    public List<string> supportedProtocols { get; set; }

    // Keys this page does not know are written back unchanged.
    [JsonExtensionData]
    public IDictionary<string, JToken> extensionData { get; set; }
}
