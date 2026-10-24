namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// One endpoint of the online DPI checker suite. The property names follow the JSON of the suite.
/// </summary>
internal sealed class DpiSuiteEntry
{
    public string id { get; set; }
    public string provider { get; set; }
    public string country { get; set; }
    public string host { get; set; }
}
