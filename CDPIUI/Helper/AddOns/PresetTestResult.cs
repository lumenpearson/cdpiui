using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using WinUI3Localizer;

namespace CDPIUI.Helper.AddOns;

public class PresetTestResult
{
    public ConfigItem Preset;
    public string PresetName;
    public string PackName;
    public int HttpOk;
    public int HttpErr;
    public int Unsupported;
    public int PingOk;
    public int PingFail;
    public int DpiOk;
    public int DpiFail;
    public int DpiUnsupported;
    public int DpiBlocked;
    public bool Failed;

    public int GetScore(PresetTestType testType)
    {
        if (Failed)
            return int.MinValue;
        if (testType == PresetTestType.Standard)
            return HttpOk * 1000 + PingOk * 10 - HttpErr * 100 - PingFail * 5;
        return DpiOk * 1000 - DpiBlocked * 5000 - DpiFail * 10;
    }

    public string GetMetricsSummary(PresetTestType testType, ILocalizer localizer)
    {
        if (Failed)
            return localizer.GetLocalizedString("PT_FailedToStart");

        if (testType == PresetTestType.Standard)
        {
            return string.Format(localizer.GetLocalizedString("PT_MetricsStandard"),
                HttpOk, PingOk, HttpErr);
        }

        return string.Format(localizer.GetLocalizedString("PT_MetricsDpi"),
            DpiOk, DpiBlocked, DpiFail);
    }
}
