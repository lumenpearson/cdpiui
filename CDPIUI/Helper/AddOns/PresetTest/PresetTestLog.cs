using System;
using System.Collections.Generic;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// Writes the colored lines of the preset test log and looks up its localized texts.
/// </summary>
internal sealed class PresetTestLog
{
    private readonly IProgress<List<TestLogSegment>> _log;

    public PresetTestLog(IProgress<List<TestLogSegment>> log)
    {
        _log = log;
    }

    public static string Text(string key) => Localizer.Get().GetLocalizedString(key);

    public void Line(params TestLogSegment[] segments) => _log.Report(segments.ToList());

    public void Report(List<TestLogSegment> segments) => _log.Report(segments);

    public void Tagged(string tagKey, TestLogColor tagColor, string text)
    {
        Line(
            new TestLogSegment($"[{Text(tagKey)}] ", tagColor),
            new TestLogSegment(text));
    }
}
