#nullable enable

using System.Collections.Generic;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Helper.CreateConfigHelper;

// Presentation data without WinUI objects; the adapter supplies fonts, brushes and visibility.
internal sealed record ConfigMakerPresetFileNode(
    string DisplayName,
    string ToolTip,
    ConfigMakerPresetFileInfo? File = null,
    bool IsMissing = false,
    bool IsExpanded = false)
{
    public List<ConfigMakerPresetFileNode> Children { get; } = [];
}

internal sealed record ConfigMakerPresetOptionNode(
    string Title,
    string Description = "",
    bool IsCode = false,
    ConfigCommandOption? Source = null,
    bool IsExpanded = false)
{
    public List<ConfigMakerPresetOptionNode> Children { get; } = [];
}
