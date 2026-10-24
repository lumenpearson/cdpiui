using CDPIUI.Controls.Dialogs.ComponentSettings;

namespace CDPIUI.ViewModels.Components;

/// <summary>The site list the user chose to use instead of a list of a configuration.</summary>
public sealed record SiteListReplacement(FileSelectionType SelectionType, string NewFileName);
