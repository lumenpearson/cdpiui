using System.Collections.Generic;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>The parts of the BlockCheck2 result page that only the page can do.</summary>
public interface IBlockCheck2ResultPageView
{
    /// <summary>The strategy selected in the strategy list.</summary>
    BlockCheck2StrategyEvidenceItem? SelectedStrategy { get; }

    /// <summary>Selects the first strategy of the list.</summary>
    void SelectFirstStrategy();

    int SelectedAssignmentIndex { get; }

    BlockCheck2ManualAssignmentItem? SelectedAssignment { get; set; }

    /// <summary>Shows the preset command line in the preview editor.</summary>
    void SetPresetCommandText(string commandText);

    /// <summary>Tells the menu bar that the state of its items changed.</summary>
    void NotifyMenuStateChanged();

    void ShowStatus(BlockCheck2NoticeSeverity severity, string title, string message);

    /// <summary>Asks which site list to use; <see langword="null"/> when the user cancels.</summary>
    Task<string?> ChooseSiteListAsync(IReadOnlyList<string> paths);

    Task<bool> ConfirmReplaceRunningComponentAsync();

    /// <summary>Asks for the file to save to; <see langword="null"/> when the user cancels.</summary>
    string? PickSaveFile(string title, string fileName, string filter);

    void CopyToClipboard(string text);
}
