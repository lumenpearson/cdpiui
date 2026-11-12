using System;
using System.Collections.Generic;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.ViewModels.ConfigMaker;

// The preset structure: the file tree and the tree of the option groups and profiles.
public sealed partial class ConfigMakerViewModel
{
    private readonly Zapret2PresetOptionDescriber _zapret2OptionDescriber;
    private readonly ConfigMakerPresetTreeBuilder _presetTreeBuilder;
    private readonly ConfigMakerPresetStructure<ConfigMakerTreeNode> _presetStructure;

    /// <summary>Raised when the preset files or the preset groups appear or disappear.</summary>
    public event EventHandler PresetStructureAvailabilityChanged;

    public IReadOnlyList<ConfigMakerTreeNode> PresetFileNodes
    {
        get => _presetStructure.PresetFileNodes;
        private set => _presetStructure.PresetFileNodes = value;
    }

    public IReadOnlyList<ConfigMakerTreeNode> PresetGroupNodes
    {
        get => _presetStructure.PresetGroupNodes;
        private set => _presetStructure.PresetGroupNodes = value;
    }

    public bool IsPresetStructureVisible => _presetStructure.IsPresetStructureVisible;
    public bool HasPresetFiles => _presetStructure.HasPresetFiles;
    public bool HasPresetGroups => _presetStructure.HasPresetGroups;

    private bool IsZapret2Component => string.Equals(
        ComponentId,
        HardcodedItemIds.ComponentIds[Components.Zapret2],
        StringComparison.OrdinalIgnoreCase);

    public void SetPresetStructure(
        IEnumerable<ConfigMakerPresetFileInfo> files,
        IEnumerable<ConfigMakerPresetGroupInfo> groups) => _presetStructure.Set(files, groups);

    public void ClearPresetStructure() => _presetStructure.Clear();

    private void RebuildPresetStructure() => _presetStructure.Rebuild();
}
