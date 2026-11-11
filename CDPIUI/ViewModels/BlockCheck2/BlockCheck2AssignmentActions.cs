#nullable enable

using System;
using System.Threading.Tasks;
using CDPIUI.Helper.UserExperience;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>Assignment commands and their completion notices; the model owns preset editing.</summary>
internal sealed class BlockCheck2AssignmentActions(IBlockCheck2AssignmentActionsHost host)
{
    public void AddSelectedStrategyForSite()
    {
        BlockCheck2StrategyEvidenceItem? item = host.SelectedStrategy;
        if (item == null)
        {
            return;
        }
        ShowResult(host.AddForSite(item));
    }

    public Task AddSelectedStrategyForListAsync() => SelectedItemChoiceAction.ExecuteAsync(
        () => host.SelectedStrategy, host.GetSiteListPaths, paths => host.ChooseSiteListAsync(paths),
        (item, path) => ShowResult(host.AddForSiteList(item, path)));

    public void ShowResult(bool added)
    {
        if (added)
        {
            host.ShowMessage(BlockCheck2NoticeSeverity.Success,
                host.Text("BlockCheck2AssignmentAddedTitle"), host.Text("BlockCheck2AssignmentAddedMessage"));
        }
        else
        {
            host.ShowMessage(BlockCheck2NoticeSeverity.Warning,
                host.Text("BlockCheck2AssignmentNotAddedTitle"), host.Text("BlockCheck2AssignmentDuplicateMessage"));
        }
        host.UpdateAssignmentView();
        host.UpdateActionButtons();
    }

    public void MoveSelectedAssignment(int offset)
    {
        BlockCheck2ManualAssignmentItem? selected = host.SelectedAssignment;
        if (selected != null && host.MoveAssignment(selected, offset))
        {
            host.UpdateAssignmentView();
            host.SelectedAssignment = selected;
        }
    }

    public void RemoveSelectedAssignment()
    {
        BlockCheck2ManualAssignmentItem? selected = host.SelectedAssignment;
        if (selected == null)
        {
            return;
        }
        host.RemoveAssignment(selected);
        host.UpdateAssignmentView();
    }

    public void ClearAssignments()
    {
        host.ClearAssignments();
        host.UpdateAssignmentView();
    }

    public void ApplyManualIssueQuickFix(Func<bool> apply)
    {
        if (!apply())
        {
            return;
        }
        host.ShowMessage(BlockCheck2NoticeSeverity.Success,
            host.Text("BlockCheck2ManualIssueQuickFixCompletedTitle"),
            host.Text("BlockCheck2ManualScopeOverlapAcceptedMessage"));
        host.UpdateAssignmentView();
    }
}
