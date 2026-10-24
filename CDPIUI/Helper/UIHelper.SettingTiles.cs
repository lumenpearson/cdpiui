using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace CDPIUI.Helper;

public static partial class UIHelper
{
    public enum SettingTileContentType
    {
        OnlyViewButton,
        EditViewButtons,
        ToggleSwitch,
        SiteListToggle,
        FullButton,
        Button,
        OnlyTextContent,
        ComboBoxSelector
    }
    public class ComboBoxModel
    {
        public string DisplayName { get; set; }
        public string Id { get; set; }
    }

    public class SettingTileContentDefinition
    {
        public SettingTileContentType ContentType { get; set; }

        public string ActionGlyph { get; set; }

        public string Text { get; set; }

        public string VariableName { get; set; }
        public bool InitialToggleState { get; set; }
        public string PackId { get; set; }
        public string FileName { get; set; }

        public string EditFilePath { get; set; }
        public List<string> ViewParams { get; set; }
        public List<string> PrettyViewParams { get; set; }
        public bool IsFileHardLinked { get; set; }
        public string HardLinkTargetFile { get; set; }
        public bool IsIPSet { get; set; }

        public string ClickId { get; set; }

        public List<ComboBoxModel> ComboBoxItems { get; set; }
        public string SelectedComboBoxItemId { get; set; }
    }

    public class SettingsTileItem
    {
        public string Title { get; set; }
        public bool ShowTopRectangle { get; set; }
        public IList<SettingTileContentDefinition> Contents { get; } = new List<SettingTileContentDefinition>();
    }

    public partial class SettingsTile : INotifyPropertyChanged
    {
        public string IconGlyph { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public ObservableCollection<SettingsTileItem> Items { get; } = new ObservableCollection<SettingsTileItem>();

        // The properties do not report changes; the event exists for the interface only.
        public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }
    }

    public enum ActionIds
    {
        None,
        ViewButtonClicked,
        OpenFolderClicked,
        ChangeSiteListClicked,
        EditButtonClicked,
        SwitchToggled,
        FullButtonElementClicked,
        ComboBoxSelectionChanged
    }

    public enum TileType
    {
        Basic,
        QuickWidget
    }

    static public UIElement CreateSettingTile(
        SettingsTile config,
        Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction,
        TileType tileType = TileType.Basic,
        Thickness? padding = null)
    {
        return SettingTileFactory.Create(config, executeAction, tileType, padding);
    }
}
