using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2AssignmentActionsTests
{
    [Fact]
    public void ConstructionDoesNotReadViewOrModel()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace);
    }

    [Fact]
    public async Task MissingSelectionStopsBothAddPathsBeforeAnyOtherPort()
    {
        var f = new Fixture { Item = null };
        f.Owner.AddSelectedStrategyForSite();
        await f.Owner.AddSelectedStrategyForListAsync();
        Assert.Equal(["selected", "selected"], f.Trace);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddSitePublishesLiteralNoticeThenBothUpdates(bool added)
    {
        var f = new Fixture { Added = added }; var item = f.Item;
        f.Owner.AddSelectedStrategyForSite();
        Assert.Same(item, f.AddedItem);
        Assert.Equal(new[] { "selected", "site", "text:" + Title(added), "text:" + Message(added), "show", "assignments", "buttons" }, f.Trace);
        Assert.Equal((added ? BlockCheck2NoticeSeverity.Success : BlockCheck2NoticeSeverity.Warning, Title(added), Message(added)), Assert.Single(f.Notices));
    }

    [Fact]
    public async Task NoPathsStopsWithoutChooserOrNotice()
    {
        var f = new Fixture { Paths = [] };
        await f.Owner.AddSelectedStrategyForListAsync();
        Assert.Equal(["selected", "paths"], f.Trace);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  one path  ")]
    public async Task SinglePathSkipsChooserAndKeepsItsExactText(string? path)
    {
        var f = new Fixture { Paths = [path!] };
        await f.Owner.AddSelectedStrategyForListAsync();
        Assert.Equal(path, f.AddedPath);
        Assert.DoesNotContain("choose", f.Trace);
        Assert.Same(f.Item, f.AddedItem);
        Assert.Equal("old", f.AddedModel);
    }

    // A selected row is captured, paths are copied, and the model is read only when adding after await.
    [Fact]
    public async Task PendingChoiceKeepsCapturedRowAndSnapshotButUsesCurrentModel()
    {
        var f = new Fixture(); var oldItem = f.Item; var paths = new List<string> { "A", "B" }; f.Paths = paths;
        var gate = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Choose = _ => gate.Task;
        Task task = f.Owner.AddSelectedStrategyForListAsync();
        try
        {
            Assert.False(task.IsCompleted);
            Assert.Equal(["selected", "paths", "choose"], f.Trace);
            Assert.NotSame(paths, f.ChosenPaths);
            Assert.Equal(["A", "B"], Assert.IsType<string[]>(f.ChosenPaths));
            paths.Clear(); paths.Add("changed source"); f.Item = Item("new"); f.Model = "current";
            gate.SetResult("B");
            await task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(oldItem, f.AddedItem);
            Assert.Equal("current", f.AddedModel);
            Assert.Equal("B", f.AddedPath);
            Assert.Equal(["A", "B"], f.ChosenPaths);
            Assert.Equal(1, f.Trace.Count(x => x == "selected"));
        }
        finally { gate.TrySetResult(null); await task.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Fact]
    public async Task ChooserReceivesTheActualSnapshotArray()
    {
        var f = new Fixture();
        f.Choose = paths => { var array = Assert.IsType<string[]>(paths); array[0] = "changed snapshot"; return Task.FromResult<string?>(array[0]); };
        await f.Owner.AddSelectedStrategyForListAsync();
        Assert.Equal("changed snapshot", f.AddedPath);
        Assert.Equal("A", f.Paths[0]);
    }

    [Fact]
    public async Task CancelledChoiceDoesNotAddOrPublish()
    {
        var f = new Fixture { Choose = _ => Task.FromResult<string?>(null) };
        await f.Owner.AddSelectedStrategyForListAsync();
        Assert.Equal(["selected", "paths", "choose"], f.Trace);
        Assert.Null(f.AddedItem);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("outside list")]
    public async Task NonNullChoiceIsPassedWithoutNewValidation(string path)
    {
        var f = new Fixture { Choose = _ => Task.FromResult<string?>(path) };
        await f.Owner.AddSelectedStrategyForListAsync();
        Assert.Equal(path, f.AddedPath);
        Assert.Single(f.Notices);
    }

    [Fact]
    public async Task IndependentChoicesCanFinishInReverseOrder()
    {
        var first = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture(); var a = f.Item; var b = Item("b"); int calls = 0;
        f.Choose = _ => ++calls == 1 ? first.Task : second.Task;
        Task one = f.Owner.AddSelectedStrategyForListAsync(); f.Item = b;
        Task two = f.Owner.AddSelectedStrategyForListAsync();
        try
        {
            Assert.False(one.IsCompleted); Assert.False(two.IsCompleted); Assert.Equal(2, calls);
            second.SetResult("B"); await two.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(b, f.AddedItem); Assert.False(one.IsCompleted);
            first.SetResult("A"); await one.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(a, f.AddedItem); Assert.Equal(2, f.Notices.Count);
        }
        finally
        {
            first.TrySetResult(null); second.TrySetResult(null);
            await one.WaitAsync(TimeSpan.FromSeconds(3)); await two.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Theory]
    [InlineData("selected")]
    [InlineData("paths")]
    [InlineData("choose")]
    [InlineData("list")]
    public async Task SetupAndAddFailuresAreReturnedAsFaultedTasks(string step)
    {
        var f = new Fixture(); var expected = new InvalidOperationException(step);
        f.OnStep = name => { if (name == step) throw expected; };
        Task? task = null;
        Assert.Null(Record.Exception(() => { task = f.Owner.AddSelectedStrategyForListAsync(); }));
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => task!));
        Assert.Empty(f.Notices); Assert.DoesNotContain("assignments", f.Trace);
    }

    [Fact]
    public async Task ActuallyCancelledChooserKeepsCancellationStatusWithoutNotices()
    {
        var f = new Fixture { Choose = _ => Task.FromCanceled<string?>(new CancellationToken(true)) };
        Task task = f.Owner.AddSelectedStrategyForListAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(task.IsCanceled); Assert.Empty(f.Notices); Assert.Null(f.AddedItem);
    }

    [Fact]
    public void MoveWithoutSelectionDoesNotCallModelOrView()
    {
        var f = new Fixture(); f.Owner.MoveSelectedAssignment(-20);
        Assert.Equal(["assignment"], f.Trace);
    }

    [Fact]
    public void RejectedMoveDoesNotPublishOrReselect()
    {
        var f = new Fixture { Assignment = Assignment(), Moved = false };
        f.Owner.MoveSelectedAssignment(0);
        Assert.Equal(["assignment", "move:0"], f.Trace);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(100)]
    public void SuccessfulMovePublishesBeforeRestoringCapturedSelection(int offset)
    {
        var f = new Fixture { Assignment = Assignment() }; var original = f.Assignment;
        f.OnStep = step => { if (step == "assignments") f.Assignment = Assignment(); };
        f.Owner.MoveSelectedAssignment(offset);
        Assert.Equal(new[] { "assignment", "move:" + offset, "assignments", "select" }, f.Trace);
        Assert.Same(original, f.MovedItem); Assert.Same(original, f.Assignment);
    }

    [Fact]
    public void UpdateFaultAfterMovePreventsReselection()
    {
        var f = new Fixture { Assignment = Assignment() }; var replacement = Assignment();
        var expected = new InvalidOperationException("update");
        f.OnStep = step => { if (step == "assignments") { f.Assignment = replacement; throw expected; } };
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => f.Owner.MoveSelectedAssignment(1)));
        Assert.Same(replacement, f.Assignment); Assert.DoesNotContain("select", f.Trace);
    }

    [Fact]
    public void RemoveWithoutSelectionDoesNothingElse()
    {
        var f = new Fixture(); f.Owner.RemoveSelectedAssignment(); Assert.Equal(["assignment"], f.Trace);
    }

    [Fact]
    public void RemoveAlwaysPublishesAfterModelAndUsesCapturedObject()
    {
        var f = new Fixture { Assignment = Assignment() }; var original = f.Assignment;
        f.OnStep = step => { if (step == "remove") f.Assignment = null; };
        f.Owner.RemoveSelectedAssignment();
        Assert.Equal(["assignment", "remove", "assignments"], f.Trace); Assert.Same(original, f.RemovedItem);
    }

    [Fact]
    public void ClearPublishesEvenWhenNoAssignmentIsSelected()
    {
        var f = new Fixture(); f.Owner.ClearAssignments(); Assert.Equal(["clear", "assignments"], f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuickFixOnlyPublishesWhenAccepted(bool accepted)
    {
        var f = new Fixture { Fixed = accepted };
        f.Owner.ApplyManualIssueQuickFix(() => { f.Trace.Add("fix"); return f.Fixed; });
        Assert.Equal(accepted ? new[] { "fix", "text:BlockCheck2ManualIssueQuickFixCompletedTitle", "text:BlockCheck2ManualScopeOverlapAcceptedMessage", "show", "assignments" } : ["fix"], f.Trace);
        if (accepted) Assert.Equal((BlockCheck2NoticeSeverity.Success, "BlockCheck2ManualIssueQuickFixCompletedTitle", "BlockCheck2ManualScopeOverlapAcceptedMessage"), Assert.Single(f.Notices));
    }

    [Theory]
    [InlineData("text:BlockCheck2AssignmentAddedTitle", 0)]
    [InlineData("text:BlockCheck2AssignmentAddedMessage", 0)]
    [InlineData("show", 1)]
    [InlineData("assignments", 1)]
    [InlineData("buttons", 1)]
    public void NoticeAndUpdateFaultsKeepIdentityAndStopTheRemainingEffects(string step, int notices)
    {
        var f = new Fixture(); var error = new InvalidOperationException(step);
        f.OnStep = name => { if (name == step) throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => f.Owner.ShowResult(true)));
        Assert.Equal(step, f.Trace.Last()); Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void SynchronousModelFaultDoesNotBecomeATaskOrShowNotice()
    {
        var f = new Fixture(); var error = new InvalidOperationException("owned model fault");
        f.OnStep = step => { if (step == "site") throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(f.Owner.AddSelectedStrategyForSite));
        Assert.Empty(f.Notices); Assert.DoesNotContain("assignments", f.Trace);
    }

    [Theory]
    [InlineData("apply")]
    [InlineData("text:BlockCheck2ManualIssueQuickFixCompletedTitle")]
    [InlineData("text:BlockCheck2ManualScopeOverlapAcceptedMessage")]
    [InlineData("show")]
    [InlineData("assignments")]
    public void QuickFixFaultsRemainSynchronousAndStopSubsequentEffects(string step)
    {
        var f = new Fixture(); var error = new InvalidOperationException(step);
        f.OnStep = name => { if (name == step) throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => f.Owner.ApplyManualIssueQuickFix(() =>
        {
            f.Trace.Add("apply"); if (step == "apply") throw error; return true;
        })));
        Assert.Equal(step, f.Trace.Last()); Assert.DoesNotContain("buttons", f.Trace);
    }

    [Fact]
    public void ResultNoticeAllowsReentryWithoutSuppressingOuterUpdates()
    {
        var f = new Fixture(); bool nested = false;
        f.OnStep = step => { if (step == "assignments" && !nested) { nested = true; f.Owner.ShowResult(false); } };
        f.Owner.ShowResult(true);
        Assert.Equal(new[] { "text:" + Title(true), "text:" + Message(true), "show", "assignments",
            "text:" + Title(false), "text:" + Message(false), "show", "assignments", "buttons", "buttons" }, f.Trace);
    }

    private static string Title(bool success) => success ? "BlockCheck2AssignmentAddedTitle" : "BlockCheck2AssignmentNotAddedTitle";
    private static string Message(bool success) => success ? "BlockCheck2AssignmentAddedMessage" : "BlockCheck2AssignmentDuplicateMessage";
    private static BlockCheck2StrategyEvidenceItem Item(string id) => new(new() { Strategy = new() { Id = id }, Target = new() { Id = id, Host = "fixture.invalid" } });
    private static BlockCheck2ManualAssignmentItem Assignment() => new(new("s", "t", BlockCheckManualScopeKind.Site), new() { Id = "s" }, new() { Id = "t", Host = "fixture.invalid" }, "--args");

    private sealed class Fixture : IBlockCheck2AssignmentActionsHost
    {
        internal readonly BlockCheck2AssignmentActions Owner;
        internal readonly List<string> Trace = [];
        internal readonly List<(BlockCheck2NoticeSeverity, string, string)> Notices = [];
        internal BlockCheck2StrategyEvidenceItem? Item = BlockCheck2AssignmentActionsTests.Item("old"), AddedItem;
        internal BlockCheck2ManualAssignmentItem? Assignment, MovedItem, RemovedItem;
        internal IReadOnlyList<string> Paths = new[] { "A", "B" };
        internal IReadOnlyList<string>? ChosenPaths;
        internal string Model = "old";
        internal string? AddedModel, AddedPath;
        internal bool Added = true, Moved = true, Fixed = true;
        internal Action<string>? OnStep;
        internal Func<IReadOnlyList<string>, Task<string?>> Choose = _ => Task.FromResult<string?>("B");
        internal Fixture() => Owner = new(this);
        private void Step(string name) { Trace.Add(name); OnStep?.Invoke(name); }
        public BlockCheck2StrategyEvidenceItem? SelectedStrategy { get { Step("selected"); return Item; } }
        public IReadOnlyList<string> GetSiteListPaths(BlockCheck2StrategyEvidenceItem item) { Step("paths"); return Paths; }
        public Task<string?> ChooseSiteListAsync(IReadOnlyList<string> paths) { ChosenPaths = paths; Step("choose"); return Choose(paths); }
        public bool AddForSite(BlockCheck2StrategyEvidenceItem item) { AddedItem = item; AddedModel = Model; Step("site"); return Added; }
        public bool AddForSiteList(BlockCheck2StrategyEvidenceItem item, string path) { AddedItem = item; AddedPath = path; AddedModel = Model; Step("list"); return Added; }
        public BlockCheck2ManualAssignmentItem? SelectedAssignment { get { Step("assignment"); return Assignment; } set { Assignment = value; Step("select"); } }
        public bool MoveAssignment(BlockCheck2ManualAssignmentItem item, int offset) { MovedItem = item; Step("move:" + offset); return Moved; }
        public void RemoveAssignment(BlockCheck2ManualAssignmentItem item) { RemovedItem = item; Step("remove"); }
        public void ClearAssignments() => Step("clear");
        public void UpdateAssignmentView() => Step("assignments");
        public void UpdateActionButtons() => Step("buttons");
        public string Text(string key) { Step("text:" + key); return key; }
        public void ShowMessage(BlockCheck2NoticeSeverity severity, string title, string message) { Notices.Add((severity, title, message)); Step("show"); }
    }
}
