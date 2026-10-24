using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2ResultPageViewModel
{
    private bool _areAssignmentsVisible = true;
    private bool _areManualIssuesVisible = true;
    private bool _isNoManualIssuesVisible = true;
    private bool _isMoveAssignmentUpEnabled = true;
    private bool _isMoveAssignmentDownEnabled = true;
    private bool _isRemoveAssignmentEnabled = true;

    public bool AreAssignmentsVisible
    {
        get => _areAssignmentsVisible;
        private set => SetProperty(ref _areAssignmentsVisible, value);
    }

    public bool IsEmptyAssignmentsVisible => !_areAssignmentsVisible;

    public bool AreManualIssuesVisible
    {
        get => _areManualIssuesVisible;
        private set => SetProperty(ref _areManualIssuesVisible, value);
    }

    public bool IsNoManualIssuesVisible
    {
        get => _isNoManualIssuesVisible;
        private set => SetProperty(ref _isNoManualIssuesVisible, value);
    }

    public bool IsMoveAssignmentUpEnabled
    {
        get => _isMoveAssignmentUpEnabled;
        private set => SetProperty(ref _isMoveAssignmentUpEnabled, value);
    }

    public bool IsMoveAssignmentDownEnabled
    {
        get => _isMoveAssignmentDownEnabled;
        private set => SetProperty(ref _isMoveAssignmentDownEnabled, value);
    }

    public bool IsRemoveAssignmentEnabled
    {
        get => _isRemoveAssignmentEnabled;
        private set => SetProperty(ref _isRemoveAssignmentEnabled, value);
    }

    public void AddSelectedStrategyForSite()
    {
        BlockCheck2StrategyEvidenceItem? item = _view.SelectedStrategy;
        if (item == null)
        {
            return;
        }
        ShowAssignmentResult(Result.AddForSite(item));
    }

    public async Task AddSelectedStrategyForListAsync()
    {
        BlockCheck2StrategyEvidenceItem? item = _view.SelectedStrategy;
        if (item == null)
        {
            return;
        }
        string[] paths = Result.GetSiteListPaths(item).ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        string selectedPath = paths[0];
        if (paths.Length > 1)
        {
            string? path = await _view.ChooseSiteListAsync(paths);
            if (path == null)
            {
                return;
            }
            selectedPath = path;
        }
        ShowAssignmentResult(Result.AddForSiteList(item, selectedPath));
    }

    private void ShowAssignmentResult(bool added)
    {
        if (added)
        {
            ShowActionMessage(
                BlockCheck2NoticeSeverity.Success,
                Text("BlockCheck2AssignmentAddedTitle"),
                Text("BlockCheck2AssignmentAddedMessage"));
        }
        else
        {
            ShowActionMessage(
                BlockCheck2NoticeSeverity.Warning,
                Text("BlockCheck2AssignmentNotAddedTitle"),
                Text("BlockCheck2AssignmentDuplicateMessage"));
        }
        UpdateAssignmentView();
        UpdateActionButtons();
    }

    public void MoveSelectedAssignment(int offset)
    {
        BlockCheck2ManualAssignmentItem? selected = _view.SelectedAssignment;
        if (selected != null && Result.MoveAssignment(selected, offset))
        {
            UpdateAssignmentView();
            _view.SelectedAssignment = selected;
        }
    }

    public void RemoveSelectedAssignment()
    {
        BlockCheck2ManualAssignmentItem? selected = _view.SelectedAssignment;
        if (selected == null)
        {
            return;
        }
        Result.RemoveAssignment(selected);
        UpdateAssignmentView();
    }

    public void ClearAssignments()
    {
        Result.ClearManualAssignments();
        UpdateAssignmentView();
    }

    public void ApplyManualIssueQuickFix(BlockCheck2ManualIssueItem issue)
    {
        if (!Result.ApplyManualIssueQuickFix(issue))
        {
            return;
        }

        ShowActionMessage(
            BlockCheck2NoticeSeverity.Success,
            Text("BlockCheck2ManualIssueQuickFixCompletedTitle"),
            Text("BlockCheck2ManualScopeOverlapAcceptedMessage"));
        UpdateAssignmentView();
    }

    private void UpdateAssignmentView()
    {
        _areAssignmentsVisible = Result.ManualAssignments.Count > 0;
        OnPropertiesChanged(nameof(IsEmptyAssignmentsVisible), nameof(AreAssignmentsVisible));
        AreManualIssuesVisible = Result.ManualIssues.Count > 0;
        IsNoManualIssuesVisible = Result.ManualIssues.Count == 0;

        _view.SetPresetCommandText(ComponentCommandLineFormatter.FormatByFlags(
            Result.EffectivePresetArguments));
        UpdateAssignmentButtons();
        UpdateActionButtons();
    }

    public void UpdateAssignmentButtons()
    {
        int index = _view.SelectedAssignmentIndex;
        IsMoveAssignmentUpEnabled = index > 0;
        IsMoveAssignmentDownEnabled = index >= 0 && index < Result.ManualAssignments.Count - 1;
        IsRemoveAssignmentEnabled = index >= 0;
    }
}
