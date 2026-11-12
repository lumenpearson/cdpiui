using System.Collections.Specialized;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerVariableActionsTests
{
    [Fact]
    public void ConstructorOnlyStoresDocumentAndHost()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace);
        Assert.Empty(f.Host.Edits);
        Assert.Empty(f.Document.Variables);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullEditAndRemoveDoNothing(bool edit)
    {
        var f = new Fixture();
        if (edit) await f.Actions.EditVariableAsync(null); else await f.Actions.RemoveVariableAsync(null);
        Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledDialogDoesNotChangeDocumentOrRefresh(bool create)
    {
        var f = new Fixture(); var old = f.Add("Old");
        f.Host.Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(null);
        if (create) await f.Actions.CreateVariableAsync(); else await f.Actions.EditVariableAsync(old);
        Assert.Same(old, Assert.Single(f.Document.Variables));
        Assert.Equal(new[] { create ? "edit:<new>" : "edit:Old" }, f.Trace);
    }

    [Fact]
    public async Task CreateWaitsThenAddsExactVariableBeforeAttachShowRebuildAndDiagnostics()
    {
        var f = new Fixture(); var pending = Pending(); var value = Variable("Added");
        f.Host.Edit = (_, _) => pending.Task;
        var task = f.Actions.CreateVariableAsync();
        Assert.False(task.IsCompleted);
        Assert.Empty(f.Document.Variables);
        var call = Assert.Single(f.Host.Edits);
        Assert.Same(f.Document, call.Document); Assert.Null(call.Variable);
        pending.SetResult(new(value, "ignored"));
        await task;
        Assert.Same(value, Assert.Single(f.Document.Variables));
        Assert.Equal(new[] { "edit:<new>", "content", "variables:Add", "attach", "show", "rebuild", "diagnostics" }, f.Trace);
    }

    [Fact]
    public async Task CreateUsesCurrentCollectionAfterDialogAndDoesNotCloneOrValidateResult()
    {
        var f = new Fixture(); f.Add("Old"); var pending = Pending();
        f.Host.Edit = (_, _) => pending.Task;
        var task = f.Actions.CreateVariableAsync();
        f.Document.Variables.Clear();
        var duplicate = Variable("not-valid-name"); f.Document.Variables.Add(duplicate);
        f.Trace.Clear();
        pending.SetResult(new(duplicate, ""));
        await task;
        Assert.Equal(2, f.Document.Variables.Count);
        Assert.All(f.Document.Variables, value => Assert.Same(duplicate, value));
        Assert.Equal(new[] { "content", "variables:Add", "attach", "show", "rebuild", "diagnostics" }, f.Trace);
    }

    [Theory]
    [InlineData("Same", "Same", false)]
    [InlineData("Same", "same", true)]
    [InlineData("Different", "New", true)]
    public async Task EditRenamesByResultOriginalNameOrdinalAndPreservesEventOrder(string original, string next, bool rename)
    {
        var f = new Fixture(); var old = f.Add("Same");
        f.Document.CommandText = "%Same% %Different%"; f.Trace.Clear();
        var value = Variable(next);
        f.Host.Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(new(value, original));
        await f.Actions.EditVariableAsync(old);
        Assert.Same(f.Document, Assert.Single(f.Host.Edits).Document);
        Assert.Same(old, f.Host.Edits[0].Variable);
        Assert.Same(value, Assert.Single(f.Document.Variables));
        string expectedText = original == "Different" ? "%Same% %New%" : rename ? "%same% %Different%" : "%Same% %Different%";
        Assert.Equal(expectedText, f.Document.CommandText);
        Assert.Equal(rename
            ? new[] { "edit:Same", "content", "set:" + expectedText, "content", "variables:Replace", "attach", "rebuild", "diagnostics" }
            : new[] { "edit:Same", "content", "variables:Replace", "attach", "rebuild", "diagnostics" }, f.Trace);
    }

    [Fact]
    public async Task EditFindsTheOriginalObjectAtItsCurrentIndexAfterAwait()
    {
        var f = new Fixture(); var old = f.Add("Old"); var other = f.Add("Other");
        var pending = Pending(); f.Host.Edit = (_, _) => pending.Task;
        var task = f.Actions.EditVariableAsync(old);
        f.Document.Variables.Move(0, 1); f.Trace.Clear();
        var value = Variable("Old"); pending.SetResult(new(value, "Old")); await task;
        Assert.Equal(new[] { other, value }, f.Document.Variables);
        Assert.Equal(new[] { "content", "variables:Replace", "attach", "rebuild", "diagnostics" }, f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditStillAsksForMissingObjectButDoesNotReplaceAnEqualIdClone(bool removeWhilePending)
    {
        var f = new Fixture(); var old = Variable("Old"); var clone = old.Clone();
        f.Document.Variables.Add(removeWhilePending ? old : clone); f.Trace.Clear();
        var pending = Pending(); f.Host.Edit = (_, _) => pending.Task;
        var task = f.Actions.EditVariableAsync(old);
        Assert.Single(f.Host.Edits);
        if (removeWhilePending) { f.Document.Variables.Clear(); f.Document.Variables.Add(clone); }
        f.Trace.Clear(); pending.SetResult(new(Variable("New"), "Old")); await task;
        Assert.Same(clone, Assert.Single(f.Document.Variables));
        Assert.Empty(f.Trace);
    }

    [Fact]
    public async Task EditorReceivesCurrentCommandAfterReferenceReplacementEvents()
    {
        var f = new Fixture(); var old = f.Add("Old"); f.Document.CommandText = "%Old%"; f.Trace.Clear();
        bool entered = false;
        f.Document.ContentChanged += (_, _) =>
        {
            if (entered || f.Document.CommandText != "%New%") return;
            entered = true; f.Document.CommandText = "--changed-by-observer";
        };
        f.Host.Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(new(Variable("New"), "Old"));
        await f.Actions.EditVariableAsync(old);
        Assert.Contains("set:--changed-by-observer", f.Trace);
        Assert.DoesNotContain("set:%New%", f.Trace);
    }

    [Fact]
    public async Task EditKeepsIndexComputedBeforeReferenceEventsEvenWhenObserverMovesTheOriginal()
    {
        var f = new Fixture(); var old = f.Add("Old"); f.Add("Other");
        f.Document.CommandText = "%Old%"; f.Trace.Clear();
        bool entered = false;
        f.Document.ContentChanged += (_, _) =>
        {
            if (entered || f.Document.CommandText != "%New%") return;
            entered = true; f.Document.Variables.Move(0, 1);
        };
        var value = Variable("New");
        f.Host.Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(new(value, "Old"));
        await f.Actions.EditVariableAsync(old);
        Assert.Equal(new[] { value, old }, f.Document.Variables);
        Assert.Equal("%New%", f.Document.CommandText);
    }

    [Fact]
    public async Task ObserverClearingAfterIndexLookupRetainsRenamedTextAndEditorBeforeIndexFailure()
    {
        var f = new Fixture(); var old = f.Add("Old"); f.Document.CommandText = "%Old%"; f.Trace.Clear();
        bool entered = false;
        f.Document.ContentChanged += (_, _) =>
        {
            if (entered || f.Document.CommandText != "%New%") return;
            entered = true; f.Document.Variables.Clear();
        };
        f.Host.Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(new(Variable("New"), "Old"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Actions.EditVariableAsync(old));
        Assert.Empty(f.Document.Variables);
        Assert.Equal("%New%", f.Document.CommandText);
        Assert.Contains("set:%New%", f.Trace);
        Assert.DoesNotContain("attach", f.Trace);
    }

    [Theory]
    [InlineData("%NAME%", true)]
    [InlineData("prefix %Name% suffix", true)]
    [InlineData("%Names%", false)]
    [InlineData("", false)]
    public async Task RemoveReadsHostCommandAndUsesOrdinalIgnoreCaseReference(string command, bool referenced)
    {
        var f = new Fixture(); var value = f.Add("Name");
        f.Document.CommandText = "--document-different"; f.Host.Text = command; f.Trace.Clear();
        await f.Actions.RemoveVariableAsync(value);
        Assert.Empty(f.Document.Variables);
        Assert.Equal("--document-different", f.Document.CommandText);
        Assert.Equal(referenced
            ? new[] { "command", "confirm", "content", "variables:Remove", "rebuild", "diagnostics" }
            : new[] { "command", "content", "variables:Remove", "rebuild", "diagnostics" }, f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReferencedRemovalWaitsForConfirmationAndUsesOriginalObjectAfterAwait(bool accept)
    {
        var f = new Fixture(); var value = f.Add("Name"); f.Host.Text = "%Name%";
        var confirm = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.Confirm = () => confirm.Task;
        var task = f.Actions.RemoveVariableAsync(value);
        Assert.False(task.IsCompleted);
        Assert.Same(value, Assert.Single(f.Document.Variables));
        value.Name = "Changed"; f.Host.Text = ""; f.Trace.Clear();
        confirm.SetResult(accept); await task;
        if (accept)
        {
            Assert.Empty(f.Document.Variables);
            Assert.Equal(new[] { "content", "variables:Remove", "rebuild", "diagnostics" }, f.Trace);
        }
        else { Assert.Same(value, Assert.Single(f.Document.Variables)); Assert.Empty(f.Trace); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingRemovalStillRebuildsAndDoesNotRemoveAnEqualIdClone(bool referenced)
    {
        var f = new Fixture(); var value = Variable("Name"); var clone = value.Clone();
        f.Document.Variables.Add(clone); f.Host.Text = referenced ? "%Name%" : ""; f.Trace.Clear();
        await f.Actions.RemoveVariableAsync(value);
        Assert.Same(clone, Assert.Single(f.Document.Variables));
        Assert.Equal(referenced ? new[] { "command", "confirm", "rebuild", "diagnostics" } : new[] { "command", "rebuild", "diagnostics" }, f.Trace);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InsertReadsReadOnlyBeforeNullGuardAndOnlyThenInsertsCurrentReference(bool readOnly, bool nullVariable)
    {
        var f = new Fixture(); f.Host.ReadOnly = readOnly;
        f.Actions.InsertVariableReference(nullVariable ? null : Variable("Name"));
        Assert.Equal(!readOnly && !nullVariable ? new[] { "readonly", "insert:%Name%" } : new[] { "readonly" }, f.Trace);
    }

    [Fact]
    public void ReadOnlyGetterCanChangeVariableBeforeReferenceIsReadAndItsFailureIsSynchronous()
    {
        var f = new Fixture(); var value = Variable("Before");
        f.Host.BeforeReadOnly = () => value.Name = "After";
        f.Actions.InsertVariableReference(value);
        Assert.Equal(new[] { "readonly", "insert:%After%" }, f.Trace);
        var failure = new IOException("read-only");
        f.Host.BeforeReadOnly = () => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.Actions.InsertVariableReference(null)));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("edit")]
    [InlineData("remove")]
    public async Task PortCancellationPropagatesAsCanceledTaskWithoutMutation(string operation)
    {
        var f = new Fixture(); var value = f.Add("Old"); f.Host.Text = "%Old%";
        using var cts = new CancellationTokenSource(); cts.Cancel();
        f.Host.Edit = (_, _) => Task.FromCanceled<ConfigMakerVariableEditResult?>(cts.Token);
        f.Host.Confirm = () => Task.FromCanceled<bool>(cts.Token);
        var task = operation switch
        {
            "create" => f.Actions.CreateVariableAsync(), "edit" => f.Actions.EditVariableAsync(value), _ => f.Actions.RemoveVariableAsync(value),
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(task.IsCanceled);
        Assert.Same(value, Assert.Single(f.Document.Variables));
        Assert.DoesNotContain("rebuild", f.Trace);
    }

    [Theory]
    [InlineData("attach")]
    [InlineData("show")]
    [InlineData("rebuild")]
    [InlineData("diagnostics")]
    [InlineData("content")]
    public async Task CreateFailuresRetainAddedVariableAndStopAtExactCallback(string point)
    {
        var f = new Fixture(); var value = Variable("New"); var failure = new IOException(point);
        f.Host.Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(new(value, ""));
        f.Host.FailurePoint = point; f.Host.Failure = failure;
        if (point == "content") f.Document.ContentChanged += (_, _) => throw failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Actions.CreateVariableAsync()));
        Assert.Same(value, Assert.Single(f.Document.Variables));
        if (point is "content" or "attach") Assert.DoesNotContain("show", f.Trace);
        if (point is "content" or "attach" or "show") Assert.DoesNotContain("rebuild", f.Trace);
        if (point != "diagnostics") Assert.DoesNotContain("diagnostics", f.Trace);
    }

    [Fact]
    public async Task EditAndRemoveFailuresKeepTheirOriginalPartialMutations()
    {
        var f = new Fixture(); var old = f.Add("Old"); f.Document.CommandText = "%Old%"; f.Trace.Clear();
        var failure = new IOException("editor");
        f.Host.FailurePoint = "set"; f.Host.Failure = failure;
        f.Host.Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(new(Variable("New"), "Old"));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Actions.EditVariableAsync(old)));
        Assert.Same(old, Assert.Single(f.Document.Variables));
        Assert.Equal("%New%", f.Document.CommandText);
        Assert.DoesNotContain("attach", f.Trace);
        f.Trace.Clear(); f.Host.FailurePoint = "rebuild";
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Actions.RemoveVariableAsync(old)));
        Assert.Empty(f.Document.Variables);
        Assert.DoesNotContain("diagnostics", f.Trace);
    }

    private static ConfigMakerVariableDefinition Variable(string name) => new() { Name = name, Value = "fixture" };
    private static TaskCompletionSource<ConfigMakerVariableEditResult?> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Fixture
    {
        public List<string> Trace { get; } = [];
        public ConfigMakerPresetDocument Document { get; } = new();
        public Host Host { get; }
        public ConfigMakerVariableActions Actions { get; }
        public Fixture()
        {
            Host = new(Trace); Actions = new(Document, Host);
            Document.ContentChanged += (_, _) => Trace.Add("content");
            Document.Variables.CollectionChanged += (_, args) => Trace.Add("variables:" + args.Action);
        }
        public ConfigMakerVariableDefinition Add(string name)
        {
            var value = Variable(name); Document.Variables.Add(value); Trace.Clear(); return value;
        }
    }
    private sealed record EditCall(ConfigMakerPresetDocument Document, ConfigMakerVariableDefinition? Variable);
    private sealed class Host(List<string> trace) : IConfigMakerVariableActionsHost
    {
        public List<EditCall> Edits { get; } = [];
        public Func<ConfigMakerPresetDocument, ConfigMakerVariableDefinition?, Task<ConfigMakerVariableEditResult?>> Edit = (_, _) => Task.FromResult<ConfigMakerVariableEditResult?>(null);
        public Func<Task<bool>> Confirm = () => Task.FromResult(true);
        public string Text = "";
        public bool ReadOnly;
        public Action? BeforeReadOnly;
        public string? FailurePoint;
        public Exception? Failure;
        private void Call(string point, string? detail = null)
        {
            trace.Add(detail == null ? point : point + ":" + detail);
            if (FailurePoint == point) throw Failure!;
        }
        public string CommandText { get { Call("command"); return Text; } }
        public bool IsEditorReadOnly { get { Call("readonly"); BeforeReadOnly?.Invoke(); return ReadOnly; } }
        public Task<ConfigMakerVariableEditResult?> EditVariableAsync(ConfigMakerPresetDocument document, ConfigMakerVariableDefinition? variable)
        {
            Call("edit", variable?.Name ?? "<new>"); Edits.Add(new(document, variable)); return Edit(document, variable);
        }
        public Task<bool> ConfirmRemoveReferencedVariableAsync() { Call("confirm"); return Confirm(); }
        public void SetEditorText(string text) => Call("set", text);
        public void InsertAtCursor(string reference) => Call("insert", reference);
        public void AttachExistingPresetFiles() => Call("attach");
        public void ShowVariablesTool() => Call("show");
        public void RebuildPresetStructure() => Call("rebuild");
        public void ScheduleDiagnostics() => Call("diagnostics");
    }
}
