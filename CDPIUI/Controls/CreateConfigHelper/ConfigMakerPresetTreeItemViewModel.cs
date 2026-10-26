using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed class ConfigMakerPresetTreeItemViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int SourceIndex { get; init; } = -1;
    public int SourceLength { get; init; }
    public int SourceLine { get; init; }
    public int SourceColumn { get; init; }
    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(Description)
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Thickness ContentMargin => DescriptionVisibility == Visibility.Visible
        ? new Thickness(2, 4, 8, 4)
        : new Thickness(2, 0, 8, 0);
    public FontFamily TitleFontFamily { get; init; } = new("Segoe UI");
}
