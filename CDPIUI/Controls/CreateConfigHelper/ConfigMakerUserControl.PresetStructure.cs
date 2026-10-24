using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ParsedPresetOption = CDPIUI.Core.ComponentServices.Helpers.Configuration.ConfigCommandOption;

namespace CDPIUI.Controls.CreateConfigHelper;

// The preset structure panels: the file tree and the tree of the option groups and profiles.
public sealed partial class ConfigMakerUserControl
{
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
        SetPresetFilesPanelVisible(_hasPresetFiles);
    }

    public void ClearPresetStructure()
    {
        _usesExplicitPresetStructure = false;
        _explicitPresetFiles = [];
        _explicitPresetGroups = [];
        RebuildPresetStructure();
    }

    public void SetPresetFilesPanelVisible(bool visible)
    {
        PresetFilesPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PresetFilesContentSizer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PresetFilesColumn.Width = visible ? GridLength.Auto : new GridLength(0);
        PresetFilesSizerColumn.Width = visible ? new GridLength(12) : new GridLength(0);
        _layoutInitialized = false;
        UpdateLayoutBounds();
        PanelStateChanged?.Invoke(this, EventArgs.Empty);
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

        RebuildPresetFilesTree(files);
        RebuildPresetGroupsTree(ConfigCommandLine.ParseOptions(CommandText));

        bool filesAvailabilityChanged = _hasPresetFiles != (files.Length > 0);
        bool groupsAvailabilityChanged = _hasPresetGroups != (PresetGroupsTreeView.RootNodes.Count > 0);
        _hasPresetFiles = files.Length > 0;
        _hasPresetGroups = PresetGroupsTreeView.RootNodes.Count > 0;

        UpdatePresetGroupsTab();
        if (filesAvailabilityChanged || groupsAvailabilityChanged)
        {
            PanelStateChanged?.Invoke(this, EventArgs.Empty);
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

    private void RebuildPresetFilesTree(IReadOnlyList<ConfigMakerPresetFileInfo> fileItems)
    {
        PresetFilesTreeView.RootNodes.Clear();
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

            TreeViewNode categoryNode = new()
            {
                Content = new ConfigMakerPresetFileTreeItem
                {
                    DisplayName = PresetFileCategoryName(kind),
                    ToolTip = PresetFileCategoryName(kind),
                },
                IsExpanded = true,
            };
            foreach (ConfigMakerPresetFileInfo file in categoryFiles)
            {
                categoryNode.Children.Add(new TreeViewNode
                {
                    Content = CreatePresetFileTreeItem(file),
                });
            }
            PresetFilesTreeView.RootNodes.Add(categoryNode);
        }
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

    private void RebuildPresetGroupsTree(IReadOnlyList<ParsedPresetOption> options)
    {
        PresetGroupsTreeView.RootNodes.Clear();
        if (options.Count == 0)
        {
            AddExplicitPresetGroupsFallback();
            return;
        }

        int firstProfileOption = ConfigMakerPresetOptionGroups.FindFirstProfileOptionIndex(options);
        IReadOnlyList<ParsedPresetOption> common = options.Take(firstProfileOption).ToArray();
        AddPresetOptionGroup(
            _localizer.GetLocalizedString("ConfigMakerPresetCommonFiltersGroup"),
            common.Where(option => ConfigMakerPresetOptionGroups.IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonFiltersDescription");
        AddPresetOptionGroup(
            _localizer.GetLocalizedString("ConfigMakerPresetCommonOptionsGroup"),
            common.Where(option => !ConfigMakerPresetOptionGroups.IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonOptionsDescription");

        List<List<ParsedPresetOption>> profiles =
            ConfigMakerPresetOptionGroups.SplitProfiles(options.Skip(firstProfileOption));
        for (int index = 0; index < profiles.Count; index++)
        {
            AddPresetProfileGroup(profiles[index], index);
        }

        if (PresetGroupsTreeView.RootNodes.Count == 0)
        {
            AddExplicitPresetGroupsFallback();
        }
    }

    private void AddPresetProfileGroup(IReadOnlyList<ParsedPresetOption> options, int profileIndex)
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

        TreeViewNode profileNode = new()
        {
            Content = CreatePresetTreeItem(
                name,
                GetZapret2Description("ConfigMakerZapret2ProfileDescription")),
            IsExpanded = true,
        };
        TreeViewNode filtersNode = CreateOptionNode(
            _localizer.GetLocalizedString("ConfigMakerPresetProfileFiltersGroup"),
            options.Where(option => ConfigMakerPresetOptionGroups.IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileFiltersDescription");
        TreeViewNode actionsNode = CreateOptionNode(
            _localizer.GetLocalizedString("ConfigMakerPresetProfileActionsGroup"),
            options.Where(option => !ConfigMakerPresetOptionGroups.IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileActionsDescription");
        if (filtersNode != null)
        {
            profileNode.Children.Add(filtersNode);
        }
        if (actionsNode != null)
        {
            profileNode.Children.Add(actionsNode);
        }
        if (profileNode.Children.Count > 0)
        {
            PresetGroupsTreeView.RootNodes.Add(profileNode);
        }
    }

    private void AddPresetOptionGroup(
        string name,
        IEnumerable<ParsedPresetOption> options,
        string descriptionResourceKey)
    {
        TreeViewNode node = CreateOptionNode(name, options, descriptionResourceKey);
        if (node != null)
        {
            PresetGroupsTreeView.RootNodes.Add(node);
        }
    }

    private TreeViewNode CreateOptionNode(
        string name,
        IEnumerable<ParsedPresetOption> options,
        string descriptionResourceKey)
    {
        ParsedPresetOption[] items = options.ToArray();
        if (items.Length == 0)
        {
            return null;
        }
        TreeViewNode node = new()
        {
            Content = CreatePresetTreeItem(name, GetZapret2Description(descriptionResourceKey)),
            IsExpanded = true,
        };
        foreach (ParsedPresetOption option in items)
        {
            node.Children.Add(new TreeViewNode
            {
                Content = CreatePresetTreeItem(
                    option.DisplayText,
                    GetPresetOptionDescription(option),
                    isCode: true,
                    source: option),
            });
        }
        return node;
    }

    private void AddExplicitPresetGroupsFallback()
    {
        foreach (ConfigMakerPresetGroupInfo group in _explicitPresetGroups)
        {
            TreeViewNode node = new() { Content = CreatePresetTreeItem(group.Name) };
            foreach (string detail in group.Details.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                node.Children.Add(new TreeViewNode
                {
                    Content = CreatePresetTreeItem(detail, isCode: true),
                });
            }
            PresetGroupsTreeView.RootNodes.Add(node);
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
            TitleFontFamily = new FontFamily(isCode ? ConsoleFontHelper.Instance.FontFamily.Source.ToString() : "Segoe UI"),
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

    private void UpdatePresetGroupsTab()
    {
        bool shouldShow = _hasPresetGroups || !_usesExplicitPresetStructure;
        bool containsItem = BottomSelector.Items.Contains(PresetGroupsSelectorItem);
        if (shouldShow && !containsItem)
        {
            BottomSelector.Items.Add(PresetGroupsSelectorItem);
        }
        else if (!shouldShow && containsItem)
        {
            if (BottomSelector.SelectedIndex == BottomSelector.Items.IndexOf(PresetGroupsSelectorItem))
            {
                BottomSelector.SelectIndex(0);
            }
            BottomSelector.Items.Remove(PresetGroupsSelectorItem);
        }
    }

    private string PresetFileCategoryName(ConfigMakerPresetFileKind kind) =>
        _localizer.GetLocalizedString(kind switch
        {
            ConfigMakerPresetFileKind.Library => "ConfigMakerPresetFileCategoryLibraries",
            ConfigMakerPresetFileKind.Payload => "ConfigMakerPresetFileCategoryPayloads",
            _ => "ConfigMakerPresetFileCategorySiteLists",
        });

    private void PresetGroupsTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (PresetGroupsTreeView.SelectedNode?.Content is not ConfigMakerPresetTreeItemViewModel item ||
            item.SourceIndex < 0 ||
            item.SourceLength <= 0)
        {
            return;
        }

        CommandEditor.Focus(FocusState.Programmatic);
        CommandEditor.SetCursorPosition(
            item.SourceLine,
            item.SourceColumn,
            scrollIntoView: true,
            autoClamp: true);
        CommandEditor.SetSelection(item.SourceIndex, item.SourceLength);
        CommandEditor.ScrollLineToCenter(item.SourceLine);
        e.Handled = true;
    }
}
