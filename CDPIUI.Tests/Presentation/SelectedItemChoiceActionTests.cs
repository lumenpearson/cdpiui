using CDPIUI.Helper.UserExperience;

namespace CDPIUI.Tests.Presentation;

public class SelectedItemChoiceActionTests
{
    [Fact]
    public async Task PlainObjectsAndChoiceReferencesWorkWithoutAnyUiOrDomainType()
    {
        var item = new object(); var first = new object(); var second = new object(); var replacement = new object();
        IReadOnlyList<object>? snapshot = null; List<(object, object)> applied = [];
        await SelectedItemChoiceAction.ExecuteAsync(() => item, _ => (IReadOnlyList<object>)new[] { first, second },
            paths => { snapshot = paths; return Task.FromResult<object?>(replacement); },
            (selected, choice) => applied.Add((selected, choice)));
        Assert.NotNull(snapshot); Assert.Same(first, snapshot[0]); Assert.Same(second, snapshot[1]);
        var result = Assert.Single(applied);
        Assert.Same(item, result.Item1); Assert.Same(replacement, result.Item2);
    }

    [Fact]
    public async Task NullItemSkipsAllOtherPorts()
    {
        await SelectedItemChoiceAction.ExecuteAsync<object, string>(() => null,
            _ => throw new InvalidOperationException("choices"), _ => throw new InvalidOperationException("choose"),
            (_, _) => throw new InvalidOperationException("apply"));
    }

    [Fact]
    public async Task SingleChoiceSkipsChooserButAppliesTheSameObjects()
    {
        var item = new object(); var choice = new object(); List<(object, object)> applied = [];
        await SelectedItemChoiceAction.ExecuteAsync(() => item, _ => (IReadOnlyList<object>)new[] { choice },
            _ => throw new InvalidOperationException("choose"),
            (selected, result) => applied.Add((selected, result)));
        var result = Assert.Single(applied);
        Assert.Same(item, result.Item1); Assert.Same(choice, result.Item2);
    }

    [Fact]
    public async Task SelectorFaultIsCapturedInReturnedTask()
    {
        var error = new InvalidOperationException("owned selector"); Task? task = null;
        Assert.Null(Record.Exception(() => { task = SelectedItemChoiceAction.ExecuteAsync<object, object>(() => throw error,
            _ => [], _ => Task.FromResult<object?>(null), (_, _) => { }); }));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => task!));
    }
}
