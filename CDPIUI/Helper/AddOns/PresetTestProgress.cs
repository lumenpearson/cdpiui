using System;

namespace CDPIUI.Helper.AddOns;

public class PresetTestProgress
{
    public int CompletedPresets;
    public int TotalPresets;
    public int CurrentIndex;
    public string CurrentPresetName;
    public double Percent;
    public TimeSpan? EstimatedRemaining;
}
