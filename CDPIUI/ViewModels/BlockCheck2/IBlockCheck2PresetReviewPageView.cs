using System.Threading.Tasks;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>The parts of the BlockCheck2 preset review page that only the page can do.</summary>
public interface IBlockCheck2PresetReviewPageView
{
    /// <summary>Shows the draft in the preset editor.</summary>
    void LoadDraft(BlockCheck2PresetDraft draft);

    /// <summary>Switches the preset editor between the structured and the expert editor.</summary>
    void SetExpertEditing(bool enabled);

    Task<bool> ConfirmExpertEditingAsync();

    Task<bool> ConfirmDiscardExpertChangesAsync();

    /// <summary>Asks for the name of the preset to save; <see langword="null"/> when the user cancels.</summary>
    Task<string?> AskPresetNameAsync();

    /// <summary>Shows that the preset was saved and closes or leaves the page as the user chooses.</summary>
    Task ShowSaveCompletedAsync();

    /// <summary>Tells the menu bar that the state of its items changed.</summary>
    void NotifyMenuStateChanged();

    void ShowStatus(BlockCheck2NoticeSeverity severity, string title, string message);
}
