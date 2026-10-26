namespace CDPIUI.Views.Troubleshooting;

public class DiagnosticResultModel
{
    public bool IsHighlighted { get; set; }
    public string CriterionName { get; set; }
    public string CurrentValue { get; set; }
    public string TargetValue { get; set; }
    public string IconGlyph { get; set; }
    public bool IsHelpAvailable { get; set; }
    public bool IsFixAvailable { get; set; }
    public string HelpText { get; set; }
    public string HelpUrl { get; set; }
}
