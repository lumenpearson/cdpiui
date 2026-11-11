using System.Collections.Generic;
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

    public void AddSelectedStrategyForSite() => _assignmentActions.AddSelectedStrategyForSite();

    public Task AddSelectedStrategyForListAsync() => _assignmentActions.AddSelectedStrategyForListAsync();

    private void ShowAssignmentResult(bool added) => _assignmentActions.ShowResult(added);

    public void MoveSelectedAssignment(int offset) => _assignmentActions.MoveSelectedAssignment(offset);

    public void RemoveSelectedAssignment() => _assignmentActions.RemoveSelectedAssignment();

    public void ClearAssignments() => _assignmentActions.ClearAssignments();

    public void ApplyManualIssueQuickFix(BlockCheck2ManualIssueItem issue) =>
        _assignmentActions.ApplyManualIssueQuickFix(() => Result.ApplyManualIssueQuickFix(issue));

    private sealed class AssignmentActionsHost(BlockCheck2ResultPageViewModel page) : IBlockCheck2AssignmentActionsHost
    {
        public BlockCheck2StrategyEvidenceItem? SelectedStrategy => page._view.SelectedStrategy;
        public IReadOnlyList<string> GetSiteListPaths(BlockCheck2StrategyEvidenceItem item) => page.Result.GetSiteListPaths(item);
        public Task<string?> ChooseSiteListAsync(IReadOnlyList<string> paths) => page._view.ChooseSiteListAsync(paths);
        public bool AddForSite(BlockCheck2StrategyEvidenceItem item) => page.Result.AddForSite(item);
        public bool AddForSiteList(BlockCheck2StrategyEvidenceItem item, string path) => page.Result.AddForSiteList(item, path);
        public BlockCheck2ManualAssignmentItem? SelectedAssignment
        {
            get => page._view.SelectedAssignment;
            set => page._view.SelectedAssignment = value;
        }
        public bool MoveAssignment(BlockCheck2ManualAssignmentItem item, int offset) => page.Result.MoveAssignment(item, offset);
        public void RemoveAssignment(BlockCheck2ManualAssignmentItem item) => page.Result.RemoveAssignment(item);
        public void ClearAssignments() => page.Result.ClearManualAssignments();
        public void UpdateAssignmentView() => page.UpdateAssignmentView();
        public void UpdateActionButtons() => page.UpdateActionButtons();
        public string Text(string key) => page.Text(key);
        public void ShowMessage(BlockCheck2NoticeSeverity severity, string title, string message) => page.ShowActionMessage(severity, title, message);
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
