namespace CDPIUI.Helper.AddOns;

public class PresetTestTarget
{
    public string Name;
    public string Url;
    public string PingHost;

    public bool IsPing => string.IsNullOrEmpty(Url);
}
