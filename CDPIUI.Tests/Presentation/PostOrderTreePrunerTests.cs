using System.Collections.ObjectModel;
using CDPIUI.Helper.UserExperience;

namespace CDPIUI.Tests.Presentation;

public class PostOrderTreePrunerTests
{
    [Fact]
    public void PlainValueNodesNeedNoUiOrDomainTypes()
    {
        List<int> roots = [1]; Dictionary<int, IList<int>> children = new() { [1] = new List<int> { 2 }, [2] = new List<int>() };
        List<int> removed = [];
        PostOrderTreePruner.Prune(roots, value => children[value], _ => false, removed.Add);
        Assert.Equal(new[] { 2, 1 }, removed); Assert.Empty(roots); Assert.Empty(children[1]);
    }

    [Fact]
    public void EmptyListDoesNotReadAnyCallback()
    {
        PostOrderTreePruner.Prune<object>(new List<object>(), null!, null!, null!);
    }

    [Fact]
    public void RootsAndChildrenRunBackwardsWithRetentionAfterDescendants()
    {
        var a = new Node("a"); var b = new Node("b"); var c = new Node("c"); var d = new Node("d");
        a.Children.AddRange([b, c]); List<Node> roots = [a, d]; List<string> trace = [];
        PostOrderTreePruner.Prune(roots, node => { trace.Add("children:" + node.Name); return node.Children; },
            node => { trace.Add("retain:" + node.Name); return false; }, node => trace.Add("remove:" + node.Name));
        Assert.Equal(new[] { "children:d", "retain:d", "remove:d", "children:a", "children:c", "retain:c", "remove:c",
            "children:b", "retain:b", "remove:b", "retain:a", "remove:a" }, trace);
        Assert.Empty(roots); Assert.Empty(a.Children);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void ClaimedParentStillPrunesUnclaimedChildren(bool strong, int claims)
    {
        var parent = new Node("parent") { Strong = strong, Claims = claims }; var child = new Node("child");
        parent.Children.Add(child); List<Node> roots = [parent]; List<Node> removed = [];
        Run(roots, removed.Add);
        Assert.Same(parent, Assert.Single(roots)); Assert.Empty(parent.Children); Assert.Same(child, Assert.Single(removed));
    }

    [Fact]
    public void RetainedDescendantKeepsAllAncestors()
    {
        var root = new Node("root"); var branch = new Node("branch"); var leaf = new Node("leaf") { Claims = 1 };
        root.Children.Add(branch); branch.Children.Add(leaf); List<Node> roots = [root]; List<Node> removed = [];
        Run(roots, removed.Add);
        Assert.Same(root, Assert.Single(roots)); Assert.Same(branch, Assert.Single(root.Children));
        Assert.Same(leaf, Assert.Single(branch.Children)); Assert.Empty(removed);
    }

    [Fact]
    public void ChildRemovalCanAddAParentClaimBeforeRetention()
    {
        var parent = new Node("parent"); var child = new Node("child"); parent.Children.Add(child);
        List<Node> roots = [parent]; List<Node> removed = [];
        Run(roots, node => { removed.Add(node); if (ReferenceEquals(node, child)) parent.Claims++; });
        Assert.Same(child, Assert.Single(removed)); Assert.Same(parent, Assert.Single(roots)); Assert.Empty(parent.Children);
    }

    [Fact]
    public void ChildRemovalCanClearAParentClaimBeforeRetention()
    {
        var parent = new Node("parent") { Strong = true }; var child = new Node("child"); parent.Children.Add(child);
        List<Node> roots = [parent]; List<Node> removed = [];
        Run(roots, node => { removed.Add(node); if (ReferenceEquals(node, child)) parent.Strong = false; });
        Assert.Equal(new[] { child, parent }, removed); Assert.Empty(roots);
    }

    [Fact]
    public void RemovalCallbackObservesTheOriginalLogicalLists()
    {
        var parent = new Node("parent"); var child = new Node("child"); parent.Children.Add(child);
        var children = parent.Children; List<Node> roots = [parent]; int calls = 0;
        Run(roots, node =>
        {
            calls++;
            Assert.Same(parent, Assert.Single(roots)); Assert.Same(children, parent.Children);
            if (ReferenceEquals(node, child)) Assert.Same(child, Assert.Single(children));
            else Assert.Empty(children);
        });
        Assert.Equal(2, calls); Assert.Empty(roots); Assert.Empty(children);
    }

    [Fact]
    public void CallbackFaultPreservesEarlierRemovalAndTheFaultingLogicalNode()
    {
        var parent = new Node("parent"); var a = new Node("a"); var b = new Node("b"); parent.Children.AddRange([a, b]);
        List<Node> roots = [parent]; HashSet<Node> registry = [parent, a, b]; List<Node> physical = [a, b];
        var error = new InvalidOperationException("physical removal");
        var actual = Assert.Throws<InvalidOperationException>(() => Run(roots, node =>
        {
            registry.Remove(node);
            if (ReferenceEquals(node, a)) throw error;
            physical.Remove(node);
        }));
        Assert.Same(error, actual); Assert.Same(parent, Assert.Single(roots)); Assert.Same(a, Assert.Single(parent.Children));
        Assert.Same(a, Assert.Single(physical)); Assert.Equal(new[] { parent }, registry);
    }

    [Fact]
    public void ChildSelectorFaultEscapesBeforeRetentionOrRemoval()
    {
        var node = new Node("node"); List<Node> roots = [node]; var error = new InvalidOperationException("children");
        int retained = 0, removed = 0;
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => PostOrderTreePruner.Prune(roots,
            _ => throw error, _ => { retained++; return false; }, _ => removed++)));
        Assert.Equal(0, retained); Assert.Equal(0, removed); Assert.Same(node, Assert.Single(roots));
    }

    [Fact]
    public void ParentRetentionFaultKeepsAlreadyPrunedChildren()
    {
        var parent = new Node("parent"); var child = new Node("child"); parent.Children.Add(child);
        List<Node> roots = [parent]; List<Node> removed = []; var error = new InvalidOperationException("retain");
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => PostOrderTreePruner.Prune(roots, node => node.Children,
            node => ReferenceEquals(node, parent) ? throw error : false, removed.Add)));
        Assert.Same(child, Assert.Single(removed)); Assert.Same(parent, Assert.Single(roots)); Assert.Empty(parent.Children);
    }

    [Fact]
    public void LogicalRemovalFaultOccursAfterCallbackWithSameException()
    {
        var node = new Node("node"); var error = new InvalidOperationException("logical removal");
        var roots = new FailingList<Node>([node], error); List<Node> removed = [];
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => Run(roots, removed.Add)));
        Assert.Same(node, Assert.Single(roots)); Assert.Same(node, Assert.Single(removed));
    }

    [Fact]
    public void CurrentNodeIsCapturedBeforeItsCallbackChangesTheSlot()
    {
        var original = new Node("original"); var replacement = new Node("replacement");
        List<Node> roots = [original]; List<Node> removed = [];
        Run(roots, node => { removed.Add(node); roots[0] = replacement; });
        Assert.Same(original, Assert.Single(removed)); Assert.Empty(roots);
    }

    [Fact]
    public void RootAppendedDuringRemovalIsOutsideTheCapturedIndexRange()
    {
        var original = new Node("original"); var appended = new Node("appended"); List<Node> roots = [original]; List<Node> removed = [];
        Run(roots, node => { removed.Add(node); roots.Add(appended); });
        Assert.Same(original, Assert.Single(removed)); Assert.Same(appended, Assert.Single(roots));
    }

    [Fact]
    public void ChildInsertionUsesLiveIndicesWithoutRepairOrSnapshot()
    {
        var parent = new Node("parent"); var a = new Node("a"); var b = new Node("b"); var inserted = new Node("inserted");
        parent.Children.AddRange([a, b]); List<Node> roots = [parent]; List<Node> removed = [];
        Run(roots, node => { removed.Add(node); if (ReferenceEquals(node, b)) parent.Children.Insert(0, inserted); });
        Assert.Equal(new[] { b, inserted }, removed); Assert.Same(parent, Assert.Single(roots));
        Assert.Same(b, Assert.Single(parent.Children));
    }

    [Fact]
    public void ShrinkingCurrentListCanKeepOriginalIndexFailureAndPartialState()
    {
        var a = new Node("a"); var b = new Node("b"); List<Node> roots = [a, b]; List<Node> removed = [];
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(roots, node => { removed.Add(node); roots.RemoveAt(0); }));
        Assert.Same(b, Assert.Single(removed)); Assert.Same(b, Assert.Single(roots));
    }

    [Fact]
    public void ReentrantPruningIsNotSuppressedAndOuterRemovalStillUsesItsIndex()
    {
        var node = new Node("node"); List<Node> roots = [node]; List<Node> removed = []; bool entered = false;
        void Remove(Node current)
        {
            removed.Add(current);
            if (entered) return;
            entered = true; Run(roots, Remove);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(roots, Remove));
        Assert.Equal(new[] { node, node }, removed); Assert.Empty(roots);
    }

    [Fact]
    public void ChildListIsSelectedOnceAndTheSameCollectionIsMutated()
    {
        var parent = new Node("parent"); var child = new Node("child"); parent.Children.Add(child);
        List<Node> roots = [parent]; Dictionary<Node, int> reads = []; var children = parent.Children;
        PostOrderTreePruner.Prune(roots, node => { reads[node] = reads.GetValueOrDefault(node) + 1; return node.Children; },
            _ => false, _ => { });
        Assert.Equal(1, reads[parent]); Assert.Equal(1, reads[child]); Assert.Same(children, parent.Children);
        Assert.Empty(roots); Assert.Empty(children);
    }

    [Fact]
    public void EarlierRootRetentionReadsStateChangedByLaterRootRemoval()
    {
        var first = new Node("first"); var last = new Node("last"); List<Node> roots = [first, last]; List<Node> removed = [];
        Run(roots, node => { removed.Add(node); first.Strong = true; });
        Assert.Same(last, Assert.Single(removed)); Assert.Same(first, Assert.Single(roots));
    }

    private static void Run(IList<Node> roots, Action<Node> remove) =>
        PostOrderTreePruner.Prune(roots, static node => node.Children,
            static node => node.Strong || node.Claims > 0 || node.Children.Count > 0, remove);

    private sealed class Node(string name)
    {
        public string Name { get; } = name;
        public bool Strong { get; set; }
        public int Claims { get; set; }
        public List<Node> Children { get; } = [];
    }

    private sealed class FailingList<T>(IList<T> items, Exception error) : Collection<T>(items)
    {
        protected override void RemoveItem(int index) => throw error;
    }
}
