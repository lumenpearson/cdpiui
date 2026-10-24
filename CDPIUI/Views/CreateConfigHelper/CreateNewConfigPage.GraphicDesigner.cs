using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Views.CreateConfigHelper;

// The graphic designer for GoodbyeDPI, SpoofDPI and NoDPI.
public sealed partial class CreateNewConfigPage
{
    private void MainPivot_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        HandleSelectionChange();
    }

    private void HandleSelectionChange(bool showDefaultOnly = false)
    {
        ChangeBackground();
        if (MainPivot.SelectedItem == DefaultDesigner || showDefaultOnly)
        {
            StartupStringTextBox.Visibility = Visibility.Visible;
            GUIDesignerGrid.Visibility = Visibility.Collapsed;
            if (!showDefaultOnly)
                ConvertDesignerLikeSettingsToString();
        }
        else
        {
            StartupStringTextBox.Visibility = Visibility.Collapsed;
            GUIDesignerGrid.Visibility = Visibility.Visible;
            LoadDesigner();
            ConvertStringToDesignerLikeSettings();
            SearchControl.Close();
        }
    }

    private void LoadDesigner()
    {
        if (ComponentChooseComboBox.SelectedItem == null)
            return;
        string id = ((ComponentModel)ComponentChooseComboBox.SelectedItem).Id;
        if (HardcodedItemIds.ComponentIds[Components.GoodbyeDPI] == id && _nowConfigLoadedId != HardcodedItemIds.ComponentIds[Components.GoodbyeDPI])
        {
            GraphicDesignerHelper.LoadGoodbyeDPIDesignerConfig(DesignerSettingItemModels, DesignerExclusiveSettingItemModels);
            _nowConfigLoadedId = HardcodedItemIds.ComponentIds[Components.GoodbyeDPI];
        }
        else if (HardcodedItemIds.ComponentIds[Components.SpoofDPI] == id && _nowConfigLoadedId != HardcodedItemIds.ComponentIds[Components.SpoofDPI])
        {
            GraphicDesignerHelper.LoadSpoofDPIDesignerConfig(DesignerSettingItemModels, DesignerExclusiveSettingItemModels);
            _nowConfigLoadedId = HardcodedItemIds.ComponentIds[Components.SpoofDPI];
        }
        else if (HardcodedItemIds.ComponentIds[Components.NoDPI] == id && _nowConfigLoadedId != HardcodedItemIds.ComponentIds[Components.NoDPI])
        {
            string dir = DatabaseHelper.Instance.GetItemById(id)?.Directory ?? string.Empty;
            string xmlFile = Path.Combine(dir, "edannotationfile.xml");
            if (File.Exists(xmlFile))
            {
                GraphicDesignerHelper.XML_LoadDesignerConfig(xmlFile, "nodpi", DesignerSettingItemModels, DesignerExclusiveSettingItemModels);
                _nowConfigLoadedId = HardcodedItemIds.ComponentIds[Components.NoDPI];
            }
        }
    }

    private void HandleGraphicDesignerTextValueChanged(Tuple<string, string> tuple)
    {
        string guid = tuple.Item1;
        string value = tuple.Item2;

        var item = DesignerSettingItemModels.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            item.Value = value;
        }
        else
        {
            item = DesignerExclusiveSettingItemModels.FirstOrDefault(x => x.Items.FirstOrDefault(y => y.Guid == guid) != null)?.Items.FirstOrDefault(y => y.Guid == guid);

            if (item != null)
            {
                item.Value = value;
            }
        }
    }

    private void HandleGraphicDesignerBoolValueToggled(Tuple<string, bool> tuple)
    {
        string guid = tuple.Item1;
        bool _value = tuple.Item2;

        var item = DesignerSettingItemModels.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            item.IsChecked = _value;
        }
        else
        {
            item = DesignerExclusiveSettingItemModels.FirstOrDefault(x => x.Items.FirstOrDefault(y => y.Guid == guid) != null)?.Items.FirstOrDefault(y => y.Guid == guid);
            if (item != null)
            {
                item.IsChecked = _value;
            }
        }
    }

    private void HandleGraphicDesignerSelectedGuidChanged(Tuple<string, string> tuple)
    {
        string guid = tuple.Item1;
        string selGuid = tuple.Item2;
        var item = DesignerExclusiveSettingItemModels.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            Debug.WriteLine(selGuid);
            item.SelectedItemGuid = selGuid;
        }
    }

    private void ConvertStringToDesignerLikeSettings()
    {
        StartupStringTextBox.Text = StartupStringTextBox.Text.Replace("\n", " ");
        AdditionalSettingsTextBox.Text = GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(DesignerSettingItemModels, DesignerExclusiveSettingItemModels, StartupStringTextBox.Text);
    }

    private void ConvertDesignerLikeSettingsToString()
    {
        if (ComponentChooseComboBox.SelectedItem != null && !_designerSupportedComponentIds.Contains(((ComponentModel)ComponentChooseComboBox.SelectedItem).Id))
        {
            return;
        }

        StartupStringTextBox.Text = GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(DesignerSettingItemModels, DesignerExclusiveSettingItemModels, AdditionalSettingsTextBox.Text);
        StartupStringTextBox.Text = StartupStringTextBox.Text.Replace("\n", " ");
    }
}
