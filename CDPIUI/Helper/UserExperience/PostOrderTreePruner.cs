using System;
using System.Collections.Generic;

namespace CDPIUI.Helper.UserExperience;

/// <summary>Prunes live lists from the last child upwards, with removal before logical mutation.</summary>
internal static class PostOrderTreePruner
{
    public static void Prune<TNode>(IList<TNode> nodes, Func<TNode, IList<TNode>> children,
        Func<TNode, bool> retain, Action<TNode> remove)
    {
        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            var node = nodes[index];
            Prune(children(node), children, retain, remove);
            if (retain(node))
                continue;

            remove(node);
            nodes.RemoveAt(index);
        }
    }
}
