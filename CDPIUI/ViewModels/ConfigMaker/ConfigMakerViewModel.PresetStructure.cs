using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using ParsedPresetOption = CDPIUI.Core.ComponentServices.Helpers.Configuration.ConfigCommandOption;

namespace CDPIUI.ViewModels.ConfigMaker;

// The preset structure: the file tree and the tree of the option groups and profiles.
public sealed partial class ConfigMakerViewModel
{
    private readonly Zapret2PresetOptionDescriber _zapret2OptionDescriber;
    private bool _hasPresetFiles;
    private bool _hasPresetGroups;
    private bool _usesExplicitPresetStructure;
    private ConfigMakerPresetFileInfo[] _explicitPresetFiles = [];
    private ConfigMakerPresetGroupInfo[] _explicitPresetGroups = [];
    private IReadOnlyList<ConfigMakerTreeNode> _presetFileNodes = [];
    private IReadOnlyList<ConfigMakerTreeNode> _presetGroupNodes = [];

    /// <summary>Raised when the preset files or the preset groups appear or disappear.</summary>
    public event EventHandler PresetStructureAvailabilityChanged;

    public IReadOnlyList<ConfigMakerTreeNode> PresetFileNodes
    {
        get => _presetFileNodes;
        private set => SetProperty(ref _presetFileNodes, value);
    }

    public IReadOnlyList<ConfigMakerTreeNode> PresetGroupNodes
    {
        get => _presetGroupNodes;
        private set => SetProperty(ref _presetGroupNodes, value);
    }

    public bool IsPresetStructureVisible => _hasPresetFiles || HasPresetGroups;
    public bool HasPresetFiles => _hasPresetFiles;
    public bool HasPresetGroups => _hasPresetGroups || !_usesExplicitPresetStructure;

    private bool IsZapret2Component => string.Equals(
        ComponentId,
        HardcodedItemIds.ComponentIds[Components.Zapret2],
        StringComparison.OrdinalIgnoreCase);

    public void SetPresetStructure(
        IEnumerable<ConfigMakerPresetFileInfo> files,
        IEnumerable<ConfigMakerPresetGroupInfo> groups)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(groups);

        _explicitPresetFiles = files
            .Where(file => !string.IsNullOrWhiteSpace(file.Path))
            .DistinctBy(file => (file.Kind, file.Path), ConfigMakerPresetFileKeyComparer.Instance)
            .ToArray();
        _explicitPresetGroups = groups.ToArray();
        _usesExplicitPresetStructure = true;
        RebuildPresetStructure();
    }

    public void ClearPresetStructure()
    {
        _usesExplicitPresetStructure = false;
        _explicitPresetFiles = [];
        _explicitPresetGroups = [];
        RebuildPresetStructure();
    }

    private void RebuildPresetStructure()
    {
        ConfigMakerPresetFileInfo[] detectedFiles = _presetResources.ExtractFiles(CommandText).ToArray();
        ConfigMakerPresetFileInfo[] baseFiles = _usesExplicitPresetStructure
            ? EnrichExplicitPresetFiles(_explicitPresetFiles, detectedFiles)
            : detectedFiles;
        ConfigMakerPresetFileInfo[] attachedFiles = _presetResources.GetAttachedFiles().ToArray();
        ConfigMakerPresetFileInfo[] files = baseFiles
            .Concat(attachedFiles)
            .DistinctBy(file => (file.Kind, file.Path), ConfigMakerPresetFileKeyComparer.Instance)
            .ToArray();

        PresetFileNodes = BuildPresetFileNodes(files);
        List<ConfigMakerTreeNode> groupNodes = BuildPresetGroupNodes(ConfigCommandLine.ParseOptions(CommandText));

        bool filesAvailabilityChanged = _hasPresetFiles != (files.Length > 0);
        bool groupsAvailabilityChanged = _hasPresetGroups != (groupNodes.Count > 0);
        _hasPresetFiles = files.Length > 0;
        _hasPresetGroups = groupNodes.Count > 0;

        // The view shows or hides the preset groups tab from HasPresetGroups when the nodes change.
        PresetGroupNodes = groupNodes;
        OnPropertiesChanged(nameof(HasPresetFiles), nameof(HasPresetGroups), nameof(IsPresetStructureVisible));
        if (filesAvailabilityChanged || groupsAvailabilityChanged)
        {
            PresetStructureAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static ConfigMakerPresetFileInfo[] EnrichExplicitPresetFiles(
        IReadOnlyList<ConfigMakerPresetFileInfo> files,
        IReadOnlyList<ConfigMakerPresetFileInfo> detectedFiles)
    {
        return files
            .Select(file =>
            {
                ConfigMakerPresetFileInfo detected = detectedFiles.FirstOrDefault(candidate =>
                    candidate.Kind == file.Kind && ConfigFileReferences.PathsEqual(candidate.Path, file.Path));
                return detected == null || !string.IsNullOrWhiteSpace(file.OptionName)
                    ? file
                    : file with { OptionName = detected.OptionName };
            })
            .ToArray();
    }

    private List<ConfigMakerTreeNode> BuildPresetFileNodes(IReadOnlyList<ConfigMakerPresetFileInfo> fileItems)
    {
        List<ConfigMakerTreeNode> nodes = [];
        foreach (ConfigMakerPresetFileKind kind in Enum.GetValues<ConfigMakerPresetFileKind>())
        {
            ConfigMakerPresetFileInfo[] categoryFiles = fileItems
                .Where(file => file.Kind == kind)
                .OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (categoryFiles.Length == 0)
            {
                continue;
            }

            ConfigMakerTreeNode categoryNode = new(
                new ConfigMakerPresetFileTreeItem
                {
                    DisplayName = PresetFileCategoryName(kind),
                    ToolTip = PresetFileCategoryName(kind),
                },
                isExpanded: true);
            foreach (ConfigMakerPresetFileInfo file in categoryFiles)
            {
                categoryNode.Children.Add(new ConfigMakerTreeNode(CreatePresetFileTreeItem(file)));
            }
            nodes.Add(categoryNode);
        }
        return nodes;
    }

    private ConfigMakerPresetFileTreeItem CreatePresetFileTreeItem(ConfigMakerPresetFileInfo file)
    {
        string resolvedPath = _presetResources.TryResolvePath(file.Path);
        bool isMissing = string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath);
        return new ConfigMakerPresetFileTreeItem
        {
            DisplayName = file.Name,
            ToolTip = isMissing
                ? string.Format(
                    _localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"),
                    resolvedPath ?? file.Path)
                : file.IsAttachedResource
                    ? $"{file.Path}{Environment.NewLine}{resolvedPath}"
                    : resolvedPath,
            File = file,
            IsMissing = isMissing,
        };
    }

    private List<ConfigMakerTreeNode> BuildPresetGroupNodes(IReadOnlyList<ParsedPresetOption> options)
    {
        List<ConfigMakerTreeNode> nodes = [];
        if (options.Count == 0)
        {
            AddExplicitPresetGroupsFallback(nodes);
            return nodes;
        }

        int firstProfileOption = ConfigMakerPresetOptionGroups.FindFirstProfileOptionIndex(options);
        IReadOnlyList<ParsedPresetOption> common = options.Take(firstProfileOption).ToArray();
        AddOptionNode(
            nodes,
            _localizer.GetLocalizedString("ConfigMakerPresetCommonFiltersGroup"),
            common.Where(option => ConfigMakerPresetOptionGroups.IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonFiltersDescription");
        AddOptionNode(
            nodes,
            _localizer.GetLocalizedString("ConfigMakerPresetCommonOptionsGroup"),
            common.Where(option => !ConfigMakerPresetOptionGroups.IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonOptionsDescription");

        List<List<ParsedPresetOption>> profiles =
            ConfigMakerPresetOptionGroups.SplitProfiles(options.Skip(firstProfileOption));
        for (int index = 0; index < profiles.Count; index++)
        {
            AddPresetProfileNode(nodes, profiles[index], index);
        }

        if (nodes.Count == 0)
        {
            AddExplicitPresetGroupsFallback(nodes);
        }
        return nodes;
    }

    private void AddPresetProfileNode(
        List<ConfigMakerTreeNode> nodes,
        IReadOnlyList<ParsedPresetOption> options,
        int profileIndex)
    {
        string parsedName = ConfigMakerPresetOptionGroups.FindProfileName(options);
        string explicitName = profileIndex < _explicitPresetGroups.Length
            ? _explicitPresetGroups[profileIndex].Name
            : string.Empty;
        string name = !string.IsNullOrWhiteSpace(explicitName)
            ? explicitName
            : !string.IsNullOrWhiteSpace(parsedName)
                ? ConfigCommandLine.Unquote(parsedName)
                : string.Format(
                    _localizer.GetLocalizedString("ConfigMakerPresetProfileFallbackName"),
                    profileIndex + 1);

        ConfigMakerTreeNode profileNode = new(
            CreatePresetTreeItem(name, GetZapret2Description("ConfigMakerZapret2ProfileDescription")),
            isExpanded: true);
        AddOptionNode(
            profileNode.Children,
            _localizer.GetLocalizedString("ConfigMakerPresetProfileFiltersGroup"),
            options.Where(option => ConfigMakerPresetOptionGroups.IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileFiltersDescription");
        AddOptionNode(
            profileNode.Children,
            _localizer.GetLocalizedString("ConfigMakerPresetProfileActionsGroup"),
            options.Where(option => !ConfigMakerPresetOptionGroups.IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileActionsDescription");
        if (profileNode.Children.Count > 0)
        {
            nodes.Add(profileNode);
        }
    }

    private void AddOptionNode(
        List<ConfigMakerTreeNode> nodes,
        string name,
        IEnumerable<ParsedPresetOption> options,
        string descriptionResourceKey)
    {
        ParsedPresetOption[] items = options.ToArray();
        if (items.Length == 0)
        {
            return;
        }
        ConfigMakerTreeNode node = new(
            CreatePresetTreeItem(name, GetZapret2Description(descriptionResourceKey)),
            isExpanded: true);
        foreach (ParsedPresetOption option in items)
        {
            node.Children.Add(new ConfigMakerTreeNode(CreatePresetTreeItem(
                option.DisplayText,
                GetPresetOptionDescription(option),
                isCode: true,
                source: option)));
        }
        nodes.Add(node);
    }

    private void AddExplicitPresetGroupsFallback(List<ConfigMakerTreeNode> nodes)
    {
        foreach (ConfigMakerPresetGroupInfo group in _explicitPresetGroups)
        {
            ConfigMakerTreeNode node = new(CreatePresetTreeItem(group.Name));
            foreach (string detail in group.Details.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                node.Children.Add(new ConfigMakerTreeNode(CreatePresetTreeItem(detail, isCode: true)));
            }
            nodes.Add(node);
        }
    }

    private static ConfigMakerPresetTreeItemViewModel CreatePresetTreeItem(
        string title,
        string description = "",
        bool isCode = false,
        ParsedPresetOption source = null) => new()
        {
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            IsCode = isCode,
            SourceIndex = source?.SourceIndex ?? -1,
            SourceLength = source?.SourceLength ?? 0,
            SourceLine = source?.SourceLine ?? 0,
            SourceColumn = source?.SourceColumn ?? 0,
        };

    private string GetZapret2Description(string resourceKey) => IsZapret2Component &&
        !string.IsNullOrWhiteSpace(resourceKey)
            ? _localizer.GetLocalizedString(resourceKey)
            : string.Empty;

    private string GetPresetOptionDescription(ParsedPresetOption option) => IsZapret2Component
        ? _zapret2OptionDescriber.Describe(option, _helpDocument.Options)
        : string.Empty;

    private string PresetFileCategoryName(ConfigMakerPresetFileKind kind) =>
        _localizer.GetLocalizedString(kind switch
        {
            ConfigMakerPresetFileKind.Library => "ConfigMakerPresetFileCategoryLibraries",
            ConfigMakerPresetFileKind.Payload => "ConfigMakerPresetFileCategoryPayloads",
            _ => "ConfigMakerPresetFileCategorySiteLists",
        });
}
