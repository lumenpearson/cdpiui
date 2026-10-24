using System.Collections.Generic;

namespace CDPIUI.ViewModels.ConfigMaker;

/// <summary>A node of a preset tree; the view turns it into a tree view node.</summary>
public sealed class ConfigMakerTreeNode
{
    public ConfigMakerTreeNode(object content, bool isExpanded = false)
    {
        Content = content;
        IsExpanded = isExpanded;
    }

    public object Content { get; }
    public bool IsExpanded { get; }
    public List<ConfigMakerTreeNode> Children { get; } = [];
}
