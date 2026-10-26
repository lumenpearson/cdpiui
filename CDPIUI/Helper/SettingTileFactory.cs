using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.Helper;

/// <summary>
/// Builds the setting tile controls described by <see cref="SettingsTile"/>.
/// </summary>
internal static class SettingTileFactory
{
    private static readonly Thickness DefaultPadding = new(15, 10, 15, 10);

    public static UIElement Create(
        SettingsTile config,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction,
        TileType tileType,
        Thickness? padding)
    {
        ILocalizer localizer = Localizer.Get();
        Thickness cardPadding = padding ?? DefaultPadding;
        var rootStack = new StackPanel();

        foreach (var list in config.Items)
        {
            rootStack.Children.Add(CreateItemElement(list, executeAction, localizer, cardPadding));
        }

        return CreateTile(config, rootStack, tileType);
    }

    private static SettingTileControlElement CreateItemElement(
        SettingsTileItem list,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction,
        ILocalizer localizer,
        Thickness cardPadding)
    {
        var element = new SettingTileControlElement
        {
            Title = list.Title,
            ShowTopRectangle = list.ShowTopRectangle,
            CardPadding = cardPadding,
        };

        var contentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        foreach (var def in list.Contents)
        {
            switch (def.ContentType)
            {
                case SettingTileContentType.OnlyViewButton:
                    contentPanel.Children.Add(CreateViewButton(list, def, executeAction));
                    break;
                case SettingTileContentType.EditViewButtons:
                    contentPanel.Children.Add(CreateEditButton(def, executeAction, localizer));
                    contentPanel.Children.Add(CreateMoreActionsButton(list, def, executeAction, localizer));
                    break;
                case SettingTileContentType.ToggleSwitch:
                    contentPanel.Children.Add(CreateToggleSwitch(def, executeAction));
                    break;
                case SettingTileContentType.FullButton:
                    ConfigureFullButton(element, def, executeAction);
                    break;
                case SettingTileContentType.OnlyTextContent:
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = def.Text,
                        TextWrapping = TextWrapping.WrapWholeWords
                    });
                    break;
                case SettingTileContentType.ComboBoxSelector:
                    contentPanel.Children.Add(CreateComboBox(def, executeAction));
                    break;
            }
        }

        element.InnerContent = contentPanel;
        return element;
    }

    private static Button CreateViewButton(
        SettingsTileItem list,
        SettingTileContentDefinition def,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction)
    {
        var viewButton = new Button { Padding = new Thickness(6) };
        viewButton.Content = new FontIcon { Glyph = "", FontSize = 16 };
        viewButton.Click += (s, e) =>
        {
            executeAction?.Invoke(ActionIds.ViewButtonClicked, [list.Title], def);
        };
        return viewButton;
    }

    private static Button CreateEditButton(
        SettingTileContentDefinition def,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction,
        ILocalizer localizer)
    {
        var editBtn = new Button { Padding = new Thickness(6) };
        editBtn.Content = new FontIcon { Glyph = "", FontSize = 16 };
        editBtn.Click += (s, e) =>
        {
            executeAction?.Invoke(ActionIds.EditButtonClicked, null, def);
        };

        ToolTipService.SetToolTip(editBtn, localizer.GetLocalizedString("Edit"));
        return editBtn;
    }

    private static Button CreateMoreActionsButton(
        SettingsTileItem list,
        SettingTileContentDefinition def,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction,
        ILocalizer localizer)
    {
        var viewBtn = new Button { Padding = new Thickness(6) };
        MenuFlyout menuFlyout = new() { Placement = FlyoutPlacementMode.TopEdgeAlignedRight };

        FontIcon folderIcon = new() { Glyph = "" };
        MenuFlyoutItem openFolderMenuItem = new() { Text = localizer.GetLocalizedString("ViewInFolderButton"), Icon = folderIcon };
        openFolderMenuItem.Click += (s, e) =>
        {
            executeAction?.Invoke(ActionIds.OpenFolderClicked, null, def);
        };
        menuFlyout.Items.Add(openFolderMenuItem);

        FontIcon changeIcon = new() { Glyph = def.IsFileHardLinked ? "" : "" };
        MenuFlyoutItem changeIconMenuItem = new()
        {
            Text = def.IsFileHardLinked ? localizer.GetLocalizedString("RevertChangeSiteList") : localizer.GetLocalizedString("ChangeSiteList"),
            Icon = changeIcon
        };
        changeIconMenuItem.Click += (s, e) =>
        {
            executeAction?.Invoke(ActionIds.ChangeSiteListClicked, [list.Title], def);
        };
        menuFlyout.Items.Add(changeIconMenuItem);

        FontIcon viewIcon = new() { Glyph = "" };
        MenuFlyoutItem viewIconMenuItem = new() { Text = localizer.GetLocalizedString("ViewAppliedFlagsForSiteList"), Icon = viewIcon };
        viewIconMenuItem.Click += (s, e) =>
        {
            executeAction?.Invoke(ActionIds.ViewButtonClicked, [list.Title], def);
        };
        menuFlyout.Items.Add(viewIconMenuItem);

        viewBtn.Flyout = menuFlyout;
        viewBtn.Content = new FontIcon { Glyph = "", FontSize = 16 };

        ToolTipService.SetToolTip(viewBtn, localizer.GetLocalizedString("ViewMoreActionsToolTip"));
        return viewBtn;
    }

    private static ToggleSwitch CreateToggleSwitch(
        SettingTileContentDefinition def,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction)
    {
        var toggle = new ToggleSwitch
        {
            IsOn = def.InitialToggleState
        };
        toggle.Toggled += (s, e) =>
        {
            executeAction?.Invoke(ActionIds.SwitchToggled, [toggle.IsOn.ToString()], def);
        };
        return toggle;
    }

    private static void ConfigureFullButton(
        SettingTileControlElement element,
        SettingTileContentDefinition def,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction)
    {
        element.ActionIconGlyph = "";
        element.IsClickEnabled = true;

        element.Description = def.Text;

        element.Click += () =>
        {
            executeAction?.Invoke(ActionIds.FullButtonElementClicked, null, def);
        };
    }

    private static ComboBox CreateComboBox(
        SettingTileContentDefinition def,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = def.ComboBoxItems,
            DisplayMemberPath = "DisplayName",
            SelectedValuePath = "Id",
            SelectedItem = def.ComboBoxItems?.FirstOrDefault(x => x.Id == def.SelectedComboBoxItemId),
            MinWidth = 220,
        };
        comboBox.SelectionChanged += (s, e) =>
        {
            if (comboBox.SelectedItem is ComboBoxModel selectedItem)
            {
                executeAction?.Invoke(
                    ActionIds.ComboBoxSelectionChanged,
                    [selectedItem.Id],
                    def);
            }
        };
        return comboBox;
    }

    private static UIElement CreateTile(SettingsTile config, StackPanel rootStack, TileType tileType)
    {
        if (tileType == TileType.Basic)
        {
            return new SettingTile
            {
                IconGlyph = config.IconGlyph,
                Title = config.Title,
                Description = config.Description,
                InnerContent = rootStack
            };
        }
        else if (tileType == TileType.QuickWidget)
        {
            return new QuickSettingWidget
            {
                IconGlyph = config.IconGlyph,
                Title = config.Title,
                Description = config.Description,
                InnerContent = rootStack
            };
        }
        else
        {
            return null;
        }
    }
}
