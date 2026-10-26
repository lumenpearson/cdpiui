using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed class ConfigMakerDiagnosticViewModel
{
    public ComponentCommandDiagnostic Source { get; set; } = new();
    public string SeverityText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Brush SeverityBrush => new SolidColorBrush(Source.Severity == ComponentCommandDiagnosticSeverity.Error
        ? Color.FromArgb(255, 196, 43, 28)
        : Color.FromArgb(255, 202, 80, 16));
}
