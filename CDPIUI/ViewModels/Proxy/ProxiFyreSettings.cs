using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CDPIUI.ViewModels.Proxy;

/// <summary>The ProxiFyre app-config.json file.</summary>
public class ProxiFyreSettings
{
    public string logLevel { get; set; } = "Error";
    public List<ProxiFyreProxyGroup> proxies { get; set; } = [];
    public List<string> excludes { get; set; } = [];

    // Keys this page does not know are written back unchanged.
    [JsonExtensionData]
    public IDictionary<string, JToken> extensionData { get; set; }
}
