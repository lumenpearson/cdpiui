using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed class ConfigMakerPresetFileTreeItem
{
    public string DisplayName { get; init; } = string.Empty;
    public string ToolTip { get; init; } = string.Empty;
    public ConfigMakerPresetFileInfo File { get; init; }
    public bool IsMissing { get; init; }
    public Brush Background => IsMissing
        ? new SolidColorBrush(Color.FromArgb(28, 255, 185, 0))
        : null;
    public Visibility ActionsVisibility => File == null
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Visibility ExistingActionsVisibility => File != null && !IsMissing
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility MissingVisibility => File != null && IsMissing
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility RemoveVisibility => File?.IsAttachedResource == true
        ? Visibility.Visible
        : Visibility.Collapsed;
}
