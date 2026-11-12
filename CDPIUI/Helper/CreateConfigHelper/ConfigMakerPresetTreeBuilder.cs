#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Helper.CreateConfigHelper;

// Builds the preset trees without accessing the view, global services or the filesystem itself.
internal sealed class ConfigMakerPresetTreeBuilder
{
    private readonly Func<string, string> _localize;
    private readonly Func<string, string> _resolvePath;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<ConfigCommandOption, string> _describeOption;

    public ConfigMakerPresetTreeBuilder(
        Func<string, string> localize,
        Func<string, string> resolvePath,
        Func<string, bool> fileExists,
        Func<ConfigCommandOption, string> describeOption)
    {
        _localize = localize;
        _resolvePath = resolvePath;
        _fileExists = fileExists;
        _describeOption = describeOption;
    }

    public IReadOnlyList<ConfigMakerPresetFileNode> BuildFiles(IReadOnlyList<ConfigMakerPresetFileInfo> files)
    {
        List<ConfigMakerPresetFileNode> nodes = [];
        foreach (ConfigMakerPresetFileKind kind in Enum.GetValues<ConfigMakerPresetFileKind>())
        {
            ConfigMakerPresetFileInfo[] categoryFiles = files
                .Where(file => file.Kind == kind)
                .OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (categoryFiles.Length == 0)
            {
                continue;
            }

            string categoryName = _localize(kind switch
            {
                ConfigMakerPresetFileKind.Library => "ConfigMakerPresetFileCategoryLibraries",
                ConfigMakerPresetFileKind.Payload => "ConfigMakerPresetFileCategoryPayloads",
                _ => "ConfigMakerPresetFileCategorySiteLists",
            });
            ConfigMakerPresetFileNode category = new(categoryName, categoryName, IsExpanded: true);
            foreach (ConfigMakerPresetFileInfo file in categoryFiles)
            {
                string resolvedPath = _resolvePath(file.Path);
                bool isMissing = string.IsNullOrWhiteSpace(resolvedPath) || !_fileExists(resolvedPath);
                string toolTip = isMissing
                    ? string.Format(_localize("ConfigMakerPresetFileMissingMessage"), resolvedPath ?? file.Path)
                    : file.IsAttachedResource
                        ? $"{file.Path}{Environment.NewLine}{resolvedPath}"
                        : resolvedPath;
                category.Children.Add(new(file.Name, toolTip, file, isMissing));
            }
            nodes.Add(category);
        }
        return nodes;
    }

    public IReadOnlyList<ConfigMakerPresetOptionNode> BuildGroups(
        IReadOnlyList<ConfigCommandOption> options,
        IReadOnlyList<ConfigMakerPresetGroupInfo> explicitGroups,
        bool isZapret2)
    {
        List<ConfigMakerPresetOptionNode> nodes = [];
        if (options.Count == 0)
        {
            AddExplicitFallback(nodes, explicitGroups);
            return nodes;
        }

        int firstProfileOption = ConfigMakerPresetOptionGroups.FindFirstProfileOptionIndex(options);
        ConfigCommandOption[] common = options.Take(firstProfileOption).ToArray();
        AddOptionNode(nodes, _localize("ConfigMakerPresetCommonFiltersGroup"),
            common.Where(option => ConfigMakerPresetOptionGroups.IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonFiltersDescription", isZapret2);
        AddOptionNode(nodes, _localize("ConfigMakerPresetCommonOptionsGroup"),
            common.Where(option => !ConfigMakerPresetOptionGroups.IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonOptionsDescription", isZapret2);

        List<List<ConfigCommandOption>> profiles = ConfigMakerPresetOptionGroups.SplitProfiles(options.Skip(firstProfileOption));
        for (int index = 0; index < profiles.Count; index++)
        {
            AddProfileNode(nodes, profiles[index], explicitGroups, index, isZapret2);
        }
        if (nodes.Count == 0)
        {
            AddExplicitFallback(nodes, explicitGroups);
        }
        return nodes;
    }

    private void AddProfileNode(
        List<ConfigMakerPresetOptionNode> nodes,
        IReadOnlyList<ConfigCommandOption> options,
        IReadOnlyList<ConfigMakerPresetGroupInfo> explicitGroups,
        int index,
        bool isZapret2)
    {
        string? parsedName = ConfigMakerPresetOptionGroups.FindProfileName(options);
        string explicitName = index < explicitGroups.Count ? explicitGroups[index].Name : string.Empty;
        string name = !string.IsNullOrWhiteSpace(explicitName)
            ? explicitName
            : !string.IsNullOrWhiteSpace(parsedName)
                ? ConfigCommandLine.Unquote(parsedName)
                : string.Format(_localize("ConfigMakerPresetProfileFallbackName"), index + 1);
        ConfigMakerPresetOptionNode profile = new(
            name, Description("ConfigMakerZapret2ProfileDescription", isZapret2), IsExpanded: true);
        AddOptionNode(profile.Children, _localize("ConfigMakerPresetProfileFiltersGroup"),
            options.Where(option => ConfigMakerPresetOptionGroups.IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileFiltersDescription", isZapret2);
        AddOptionNode(profile.Children, _localize("ConfigMakerPresetProfileActionsGroup"),
            options.Where(option => !ConfigMakerPresetOptionGroups.IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileActionsDescription", isZapret2);
        if (profile.Children.Count > 0)
        {
            nodes.Add(profile);
        }
    }

    private void AddOptionNode(
        List<ConfigMakerPresetOptionNode> nodes,
        string name,
        IEnumerable<ConfigCommandOption> options,
        string descriptionKey,
        bool isZapret2)
    {
        ConfigCommandOption[] items = options.ToArray();
        if (items.Length == 0)
        {
            return;
        }
        ConfigMakerPresetOptionNode node = new(name, Description(descriptionKey, isZapret2), IsExpanded: true);
        foreach (ConfigCommandOption option in items)
        {
            node.Children.Add(new(option.DisplayText, isZapret2 ? _describeOption(option) : string.Empty,
                IsCode: true, Source: option));
        }
        nodes.Add(node);
    }

    private static void AddExplicitFallback(
        List<ConfigMakerPresetOptionNode> nodes,
        IEnumerable<ConfigMakerPresetGroupInfo> groups)
    {
        foreach (ConfigMakerPresetGroupInfo group in groups)
        {
            ConfigMakerPresetOptionNode node = new(group.Name);
            foreach (string detail in group.Details.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                node.Children.Add(new(detail, IsCode: true));
            }
            nodes.Add(node);
        }
    }

    private string Description(string key, bool isZapret2) =>
        isZapret2 && !string.IsNullOrWhiteSpace(key) ? _localize(key) : string.Empty;
}
