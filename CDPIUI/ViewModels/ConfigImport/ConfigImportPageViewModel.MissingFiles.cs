using System.Linq;

namespace CDPIUI.ViewModels.ConfigImport;

public sealed partial class ConfigImportPageViewModel
{
    private void ShowMissingFiles()
    {
        IsLoading = false;

        GoToStep(ConfigImportStep.MissingFiles);

        NextButtonText = _localizer.GetLocalizedString("ConfigImportContinueButton");
        UpdateMissingFilesNextState();
    }

    public void UseSuggestedFile(ConfigImportMissingFileViewModel item)
    {
        item.UseSuggestion();
        MissingFiles.Remove(item);
        UpdateMissingFilesNextState();
    }

    public void ChooseReplacementFile(ConfigImportMissingFileViewModel item)
    {
        string replacementPath = _view.PickReplacementFile(item.MissingPath);
        if (replacementPath == null)
            return;

        item.UseReplacement(replacementPath);
        MissingFiles.Remove(item);
        UpdateMissingFilesNextState();
    }

    public void UseEmptyFile(ConfigImportMissingFileViewModel item)
    {
        item.UseEmptyFile();
        MissingFiles.Remove(item);
        UpdateMissingFilesNextState();
    }

    private void UpdateMissingFilesNextState() => SetNextEnabled(
        MissingFiles.Count == 0
        || (MissingFiles.Count > 0 && MissingFiles.All(item => item.IsResolved))
        || (MissingFiles.Count > 0 && MissingFiles.All(item => item.HasSuggestion)));
}
