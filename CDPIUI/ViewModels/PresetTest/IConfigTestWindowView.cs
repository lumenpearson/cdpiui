using System.Collections.Generic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.AddOns;

namespace CDPIUI.ViewModels.PresetTest;

/// <summary>The parts of the preset test window that only the window can do.</summary>
public interface IConfigTestWindowView
{
    /// <summary>The presets selected in the multiple-selection list.</summary>
    IReadOnlyList<ConfigItem> GetSelectedPresets();

    void ClearLog();

    /// <summary>Appends one colored line to the output and scrolls to it.</summary>
    void AppendLogLine(IReadOnlyList<TestLogSegment> segments);
}
