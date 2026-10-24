using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using Microsoft.UI.Xaml;

namespace CDPIUI;

public class PresetResultViewModel
{
    public ConfigItem Preset { get; set; }
    public string PresetName { get; set; }
    public string PackName { get; set; }
    public string MetricsText { get; set; }
    public bool IsBest { get; set; }
    public string RecommendedBadge { get; set; }
    public string ApplyLabel { get; set; }
    public string RunLabel { get; set; }

    public Visibility RecommendedBadgeVisibility => IsBest ? Visibility.Visible : Visibility.Collapsed;
}
