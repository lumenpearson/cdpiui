#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels.BlockCheck2;

internal interface IBlockCheck2AssignmentActionsHost
{
    BlockCheck2StrategyEvidenceItem? SelectedStrategy { get; }
    IReadOnlyList<string> GetSiteListPaths(BlockCheck2StrategyEvidenceItem item);
    Task<string?> ChooseSiteListAsync(IReadOnlyList<string> paths);
    bool AddForSite(BlockCheck2StrategyEvidenceItem item);
    bool AddForSiteList(BlockCheck2StrategyEvidenceItem item, string path);
    BlockCheck2ManualAssignmentItem? SelectedAssignment { get; set; }
    bool MoveAssignment(BlockCheck2ManualAssignmentItem item, int offset);
    void RemoveAssignment(BlockCheck2ManualAssignmentItem item);
    void ClearAssignments();
    void UpdateAssignmentView();
    void UpdateActionButtons();
    string Text(string key);
    void ShowMessage(BlockCheck2NoticeSeverity severity, string title, string message);
}
