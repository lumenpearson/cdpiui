using CDPIUI.Helper.UserExperience;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed class ConfigMakerPresetTreeItemViewModel
{
    private FontFamily _titleFontFamily;

    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsCode { get; init; }
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
    public FontFamily TitleFontFamily => _titleFontFamily ??= new FontFamily(IsCode
        ? ConsoleFontHelper.Instance.FontFamily.Source.ToString()
        : "Segoe UI");
}
