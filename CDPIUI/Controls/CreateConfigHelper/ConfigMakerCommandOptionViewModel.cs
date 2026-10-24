using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed class ConfigMakerCommandOptionViewModel
{
    public ComponentCommandHelpOption Source { get; set; } = new();
    public string DisplayName => Source.DisplayName;
    public string Description { get; set; } = string.Empty;
}
