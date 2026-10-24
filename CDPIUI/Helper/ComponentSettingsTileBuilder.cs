using System.Collections.Generic;
using System.IO;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Features;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.Helper;

/// <summary>
/// Builds the setting tile models of the component settings page for one configuration.
/// The models hold no UI elements, so they can be built off the UI thread.
/// </summary>
internal sealed class ComponentSettingsTileBuilder
{
    private readonly ConfigurationService _configHelper;
    private readonly ILocalizer _localizer;

    public ComponentSettingsTileBuilder(ConfigurationService configHelper, ILocalizer localizer)
    {
        _configHelper = configHelper;
        _localizer = localizer;
    }

    public SettingsTile BuildVariablesTile(List<VariableItem> variables, string fileName, string packId)
    {
        SettingsTile variablesItem = new()
        {
            IconGlyph = "\uE713",
            Title = _localizer.GetLocalizedString("/SettingTiles/AvailableSettings"),
            Description = _localizer.GetLocalizedString("/SettingTiles/SetupConfig")
        };

        bool showTopRectangle = false;
        foreach (var variable in variables)
        {
            SettingsTileItem settingsTileItem = new()
            {
                Title = $"{_configHelper.GetLocalizedConfigVarName(variable.name, packId)}",
                ShowTopRectangle = showTopRectangle,
            };

            settingsTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.ToggleSwitch,
                VariableName = variable.name,
                InitialToggleState = variable.value,
                PackId = packId,
                FileName = fileName,
            });

            variablesItem.Items.Add(settingsTileItem);

            showTopRectangle = true;
        }
        return variablesItem;
    }

    /// <param name="titleKey">The localization key of the tile title, under /SettingTiles/.</param>
    /// <param name="descriptionKey">The localization key of the tile description, under /SettingTiles/.</param>
    public SettingsTile BuildSiteListTile(
        List<SiteListItem> list,
        string packId,
        string titleKey,
        string descriptionKey)
    {
        SettingsTile sitelistTile = new()
        {
            IconGlyph = "\uE7C3",
            Title = _localizer.GetLocalizedString($"/SettingTiles/{titleKey}"),
            Description = _localizer.GetLocalizedString($"/SettingTiles/{descriptionKey}")
        };

        bool showTopRectangle = false;
        foreach (SiteListItem item in list)
        {
            if (item.Type == "NULL")
                continue;

            string hardLinkTargetFile = FileSystemLinksManager.IsFileLinked(packId, item.FilePath);

            string title =
                _localizer.GetLocalizedString($"/SettingTiles/{item.Type}") +
                $" {item.Name}" + (string.IsNullOrEmpty(hardLinkTargetFile) ? "" : $" ({Path.GetFileName(hardLinkTargetFile)})");

            SettingsTileItem settingsTileItem = new()
            {
                Title = title,
                ShowTopRectangle = showTopRectangle,
            };

            settingsTileItem.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = item.Type == "AutoSiteList" ? SettingTileContentType.OnlyViewButton : SettingTileContentType.EditViewButtons,
                EditFilePath = item.FilePath,
                PackId = packId,
                ViewParams = item.ApplyParams,
                PrettyViewParams = item.PrettyApplyParams,
                IsFileHardLinked = !string.IsNullOrEmpty(hardLinkTargetFile),
                HardLinkTargetFile = hardLinkTargetFile,
                IsIPSet = item.Type == "IpList"
            });

            sitelistTile.Items.Add(settingsTileItem);

            showTopRectangle = true;
        }
        return sitelistTile;
    }

    public SettingsTile BuildCommaVariablesTile(List<CommaVariableItem> commaVariables, string fileName, string packId)
    {
        SettingsTile commaVariablesWidget = new()
        {
            IconGlyph = "\uEC7A",
            Title = _localizer.GetLocalizedString("/SettingTiles/AdvancedSettings"),
            Description = _localizer.GetLocalizedString("/SettingTiles/ConfigVariablesTip"),
        };

        bool showTopRectangle = false;
        foreach (CommaVariableItem variable in commaVariables)
        {
            SettingsTileItem variableButton = new()
            {
                Title = variable.name,
                ShowTopRectangle = showTopRectangle,
            };
            variableButton.Contents.Add(new SettingTileContentDefinition
            {
                ContentType = SettingTileContentType.FullButton,
                Text = variable.value,
                VariableName = variable.name,
                PackId = packId,
                FileName = fileName,
                ClickId = "EDITCOMMAVAR"
            });
            commaVariablesWidget.Items.Add(variableButton);
            showTopRectangle = true;
        }
        return commaVariablesWidget;
    }

    public SettingsTile BuildHelpTile()
    {
        SettingsTile helpTile = new()
        {
            IconGlyph = "\uE754",
            Title = _localizer.GetLocalizedString("/Flashlight/Title"),
            Description = _localizer.GetLocalizedString("/Flashlight/DefaultTip")
        };

        SettingsTileItem helpTileItem = new()
        {
            Title = _localizer.GetLocalizedString("/Flashlight/OpenOfflineHelp"),
            ShowTopRectangle = false,
        };
        helpTileItem.Contents.Add(new SettingTileContentDefinition
        {
            ContentType = SettingTileContentType.FullButton,
            ClickId = "HELPOFFLINE"
        });

        helpTile.Items.Add(helpTileItem);

        SettingsTileItem whatConfigChooseHelpTileItem = new()
        {
            Title = _localizer.GetLocalizedString("/Flashlight/WhatConfigChooseHelp"),
            ShowTopRectangle = true,
        };
        whatConfigChooseHelpTileItem.Contents.Add(new SettingTileContentDefinition
        {
            ContentType = SettingTileContentType.FullButton,
            ClickId = "HELPOFFLINECONFIGCHOOISE"
        });

        helpTile.Items.Add(whatConfigChooseHelpTileItem);

        // TODO: add dynamic help

        return helpTile;
    }
}
