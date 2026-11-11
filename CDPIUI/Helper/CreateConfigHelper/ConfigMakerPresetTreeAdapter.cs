using System.Collections.Generic;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Helper.CreateConfigHelper;

// Keeps the WinUI content types and their existing XAML bindings at the presentation boundary.
internal static class ConfigMakerPresetTreeAdapter
{
    public static IReadOnlyList<ConfigMakerTreeNode> ToFileNodes(IEnumerable<ConfigMakerPresetFileNode> nodes) =>
        ConfigMakerPresetTreeProjection.Map(nodes, ToFileNode);

    public static IReadOnlyList<ConfigMakerTreeNode> ToOptionNodes(IEnumerable<ConfigMakerPresetOptionNode> nodes) =>
        ConfigMakerPresetTreeProjection.Map(nodes, ToOptionNode);

    private static ConfigMakerTreeNode ToFileNode(ConfigMakerPresetFileNode source)
    {
        ConfigMakerTreeNode node = new(new ConfigMakerPresetFileTreeItem
        {
            DisplayName = source.DisplayName,
            ToolTip = source.ToolTip,
            File = source.File,
            IsMissing = source.IsMissing,
        }, source.IsExpanded);
        node.Children.AddRange(source.Children.Select(ToFileNode));
        return node;
    }

    private static ConfigMakerTreeNode ToOptionNode(ConfigMakerPresetOptionNode source)
    {
        ConfigMakerTreeNode node = new(new ConfigMakerPresetTreeItemViewModel
        {
            Title = source.Title ?? string.Empty,
            Description = source.Description ?? string.Empty,
            IsCode = source.IsCode,
            SourceIndex = source.Source?.SourceIndex ?? -1,
            SourceLength = source.Source?.SourceLength ?? 0,
            SourceLine = source.Source?.SourceLine ?? 0,
            SourceColumn = source.Source?.SourceColumn ?? 0,
        }, source.IsExpanded);
        node.Children.AddRange(source.Children.Select(ToOptionNode));
        return node;
    }
}
