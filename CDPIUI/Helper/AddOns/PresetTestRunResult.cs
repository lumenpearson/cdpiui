using System;
using System.Collections.Generic;

namespace CDPIUI.Helper.AddOns;

public class PresetTestRunResult
{
    public IReadOnlyList<PresetTestResult> Ranked { get; init; } = Array.Empty<PresetTestResult>();
    public PresetTestResult Best { get; init; }
    public bool WasCancelled { get; init; }
}
