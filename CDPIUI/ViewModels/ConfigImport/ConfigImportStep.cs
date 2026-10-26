namespace CDPIUI.ViewModels.ConfigImport;

/// <summary>The steps of the preset import wizard, in the order of the page.</summary>
public enum ConfigImportStep
{
    Selection,
    Progress,
    MissingFiles,
    Completed,
    WorkDone
}
