#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Owns explicit/detected structure and publication order; projection stays at the UI boundary.
internal sealed class ConfigMakerPresetStructure<TNode> : ObservableObject
{
    private readonly Func<string> _commandText;
    private readonly Func<bool> _isZapret2;
    private readonly Func<string, IEnumerable<ConfigMakerPresetFileInfo>> _extractFiles;
    private readonly Func<IEnumerable<ConfigMakerPresetFileInfo>> _attachedFiles;
    private readonly Func<IReadOnlyList<ConfigMakerPresetFileInfo>, IReadOnlyList<TNode>> _buildFiles;
    private readonly Func<IReadOnlyList<ConfigCommandOption>, IReadOnlyList<ConfigMakerPresetGroupInfo>, bool, IReadOnlyList<TNode>> _buildGroups;
    private bool _hasPresetFiles;
    private bool _hasPresetGroups;
    private bool _usesExplicitPresetStructure;
    private ConfigMakerPresetFileInfo[] _explicitPresetFiles = [];
    private ConfigMakerPresetGroupInfo[] _explicitPresetGroups = [];
    private IReadOnlyList<TNode> _presetFileNodes = [];
    private IReadOnlyList<TNode> _presetGroupNodes = [];

    public ConfigMakerPresetStructure(Func<string> commandText, Func<bool> isZapret2,
        Func<string, IEnumerable<ConfigMakerPresetFileInfo>> extractFiles,
        Func<IEnumerable<ConfigMakerPresetFileInfo>> attachedFiles,
        Func<IReadOnlyList<ConfigMakerPresetFileInfo>, IReadOnlyList<TNode>> buildFiles,
        Func<IReadOnlyList<ConfigCommandOption>, IReadOnlyList<ConfigMakerPresetGroupInfo>, bool, IReadOnlyList<TNode>> buildGroups)
    {
        _commandText = commandText;
        _isZapret2 = isZapret2;
        _extractFiles = extractFiles;
        _attachedFiles = attachedFiles;
        _buildFiles = buildFiles;
        _buildGroups = buildGroups;
    }

    public event EventHandler? AvailabilityChanged;
    public IReadOnlyList<TNode> PresetFileNodes
    {
        get => _presetFileNodes;
        set => SetProperty(ref _presetFileNodes, value);
    }
    public IReadOnlyList<TNode> PresetGroupNodes
    {
        get => _presetGroupNodes;
        set => SetProperty(ref _presetGroupNodes, value);
    }
    public bool IsPresetStructureVisible => _hasPresetFiles || HasPresetGroups;
    public bool HasPresetFiles => _hasPresetFiles;
    public bool HasPresetGroups => _hasPresetGroups || !_usesExplicitPresetStructure;

    // File replacement rewrites the stored array directly, then requests its own rebuild.
    public bool UsesExplicitPresetStructure => _usesExplicitPresetStructure;
    public ConfigMakerPresetFileInfo[] ExplicitPresetFiles
    {
        get => _explicitPresetFiles;
        set => _explicitPresetFiles = value;
    }

    public void Set(IEnumerable<ConfigMakerPresetFileInfo> files, IEnumerable<ConfigMakerPresetGroupInfo> groups)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(groups);
        _explicitPresetFiles = files
            .Where(file => !string.IsNullOrWhiteSpace(file.Path))
            .DistinctBy(file => (file.Kind, file.Path), ConfigMakerPresetFileKeyComparer.Instance)
            .ToArray();
        _explicitPresetGroups = groups.ToArray();
        _usesExplicitPresetStructure = true;
        Rebuild();
    }

    public void Clear()
    {
        _usesExplicitPresetStructure = false;
        _explicitPresetFiles = [];
        _explicitPresetGroups = [];
        Rebuild();
    }

    public void Rebuild()
    {
        ConfigMakerPresetFileInfo[] detectedFiles = _extractFiles(_commandText()).ToArray();
        ConfigMakerPresetFileInfo[] baseFiles = _usesExplicitPresetStructure
            ? EnrichExplicitPresetFiles(_explicitPresetFiles, detectedFiles)
            : detectedFiles;
        ConfigMakerPresetFileInfo[] attachedFiles = _attachedFiles().ToArray();
        ConfigMakerPresetFileInfo[] files = baseFiles
            .Concat(attachedFiles)
            .DistinctBy(file => (file.Kind, file.Path), ConfigMakerPresetFileKeyComparer.Instance)
            .ToArray();

        PresetFileNodes = _buildFiles(files);
        IReadOnlyList<TNode> groupNodes = _buildGroups(
            ConfigCommandLine.ParseOptions(_commandText()), _explicitPresetGroups, _isZapret2());

        bool filesAvailabilityChanged = _hasPresetFiles != (files.Length > 0);
        bool groupsAvailabilityChanged = _hasPresetGroups != (groupNodes.Count > 0);
        _hasPresetFiles = files.Length > 0;
        _hasPresetGroups = groupNodes.Count > 0;

        // The view reads the new availability while handling the group-node notification.
        PresetGroupNodes = groupNodes;
        OnPropertiesChanged(nameof(HasPresetFiles), nameof(HasPresetGroups), nameof(IsPresetStructureVisible));
        if (filesAvailabilityChanged || groupsAvailabilityChanged)
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ConfigMakerPresetFileInfo[] EnrichExplicitPresetFiles(
        IReadOnlyList<ConfigMakerPresetFileInfo> files, IReadOnlyList<ConfigMakerPresetFileInfo> detectedFiles) =>
        files.Select(file =>
        {
            ConfigMakerPresetFileInfo? detected = detectedFiles.FirstOrDefault(candidate =>
                candidate.Kind == file.Kind && ConfigFileReferences.PathsEqual(candidate.Path, file.Path));
            return detected == null || !string.IsNullOrWhiteSpace(file.OptionName)
                ? file : file with { OptionName = detected.OptionName };
        }).ToArray();
}
