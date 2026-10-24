using System;
using System.IO;
using System.Linq;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Controls.CreateConfigHelper;

// The simple graphic designer for GoodbyeDPI, SpoofDPI and NoDPI.
public sealed partial class ConfigMakerUserControl
{
    private static bool IsSimpleDesignerComponent(string componentId) =>
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.GoodbyeDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.SpoofDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.NoDPI], StringComparison.OrdinalIgnoreCase);

    private void UpdateSimpleDesignerAvailability()
    {
        if (EditorViewSelector == null || SimpleDesignerSelectorItem == null)
        {
            return;
        }
        bool supported = IsSimpleDesignerSupported;
        bool contains = EditorViewSelector.Items.Contains(SimpleDesignerSelectorItem);
        if (supported && !contains)
        {
            EditorViewSelector.Items.Add(SimpleDesignerSelectorItem);
        }
        else if (!supported && contains)
        {
            if (ReferenceEquals(EditorViewSelector.SelectedItem, SimpleDesignerSelectorItem))
            {
                EditorViewSelector.SelectIndex(0);
            }
            EditorViewSelector.Items.Remove(SimpleDesignerSelectorItem);
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
        }
    }

    private void EditorViewSelector_SelectionChanged(
        object sender,
        CDPIUI.Controls.Navigation.AnimatedSelectorBarSelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OldItem, SimpleDesignerSelectorItem))
        {
            ApplySimpleDesignerToCommand();
        }
        if (ReferenceEquals(e.NewItem, SimpleDesignerSelectorItem) && _designerNeedsRefresh)
        {
            LoadSimpleDesigner();
        }
    }

    private void LoadSimpleDesigner()
    {
        if (!IsSimpleDesignerSupported)
        {
            return;
        }
        _updatingDesigner = true;
        try
        {
            AdditionalDesignerArgumentsTextBox.Text = ComponentCommandLineFormatter.ToSingleLine(CommandEditor.Text);
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            if (!TryLoadDesignerModels())
            {
                return;
            }
            AttachSimpleDesignerCommands();
            AdditionalDesignerArgumentsTextBox.Text =
                GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(
                    DesignerSettingItemModels,
                    DesignerExclusiveSettingItemModels,
                    ComponentCommandLineFormatter.ToSingleLine(CommandEditor.Text));
            _designerNeedsRefresh = false;
        }
        catch (Exception exception)
        {
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerUserControl), exception.ToString());
            ShowEditorMessage(
                string.Format(_localizer.GetLocalizedString("ConfigMakerDesignerLoadFailedMessage"), exception.Message),
                InfoBarSeverity.Warning);
        }
        finally
        {
            _updatingDesigner = false;
        }
    }

    // Returns false when the NoDPI annotation file is missing.
    private bool TryLoadDesignerModels()
    {
        if (string.Equals(
                ComponentId,
                HardcodedItemIds.ComponentIds[Components.GoodbyeDPI],
                StringComparison.OrdinalIgnoreCase))
        {
            GraphicDesignerHelper.LoadGoodbyeDPIDesignerConfig(
                DesignerSettingItemModels,
                DesignerExclusiveSettingItemModels);
            return true;
        }
        if (string.Equals(
                ComponentId,
                HardcodedItemIds.ComponentIds[Components.SpoofDPI],
                StringComparison.OrdinalIgnoreCase))
        {
            GraphicDesignerHelper.LoadSpoofDPIDesignerConfig(
                DesignerSettingItemModels,
                DesignerExclusiveSettingItemModels);
            return true;
        }

        string componentDirectory = DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory ?? string.Empty;
        string annotationPath = Path.Combine(componentDirectory, "edannotationfile.xml");
        if (!File.Exists(annotationPath))
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerSimpleDesignerAnnotationMissingMessage"),
                InfoBarSeverity.Warning);
            return false;
        }
        GraphicDesignerHelper.XML_LoadDesignerConfig(
            annotationPath,
            "nodpi",
            DesignerSettingItemModels,
            DesignerExclusiveSettingItemModels);
        return true;
    }

    private void AttachSimpleDesignerCommands()
    {
        foreach (GraphicDesignerSettingItemModel item in DesignerSettingItemModels)
        {
            item.DesignerTextValueChangedCommand = DesignerTextValueChangedCommand;
            item.DesignerBoolValueToggledCommand = DesignerBoolValueToggledCommand;
        }
        foreach (GraphicDesignerExclusiveSettingItemModel group in DesignerExclusiveSettingItemModels)
        {
            group.DesignerTextValueChangedCommand = DesignerTextValueChangedCommand;
            group.DesignerBoolValueToggledCommand = DesignerBoolValueToggledCommand;
            group.DesignerSelectedGuidChangedCommand = DesignerSelectedGuidChangedCommand;
        }
    }

    private void HandleDesignerTextValueChanged(Tuple<string, string> change)
    {
        GraphicDesignerSettingItemModel item = FindDesignerItem(change.Item1);
        if (item != null)
        {
            item.Value = change.Item2;
            ApplySimpleDesignerToCommand();
        }
    }

    private void HandleDesignerBoolValueToggled(Tuple<string, bool> change)
    {
        GraphicDesignerSettingItemModel item = FindDesignerItem(change.Item1);
        if (item != null)
        {
            item.IsChecked = change.Item2;
            ApplySimpleDesignerToCommand();
        }
    }

    private void HandleDesignerSelectedGuidChanged(Tuple<string, string> change)
    {
        GraphicDesignerExclusiveSettingItemModel group = DesignerExclusiveSettingItemModels.FirstOrDefault(item =>
            string.Equals(item.Guid, change.Item1, StringComparison.Ordinal));
        if (group != null)
        {
            group.SelectedItemGuid = change.Item2;
            ApplySimpleDesignerToCommand();
        }
    }

    private GraphicDesignerSettingItemModel FindDesignerItem(string id) =>
        DesignerSettingItemModels.FirstOrDefault(item => string.Equals(item.Guid, id, StringComparison.Ordinal)) ??
        DesignerExclusiveSettingItemModels
            .SelectMany(group => group.Items)
            .FirstOrDefault(item => string.Equals(item.Guid, id, StringComparison.Ordinal));

    private void AdditionalDesignerArgumentsTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplySimpleDesignerToCommand();

    private void ApplySimpleDesignerToCommand()
    {
        if (_updatingDesigner || _designerNeedsRefresh || !IsSimpleDesignerSupported)
        {
            return;
        }
        _updatingDesigner = true;
        try
        {
            string command = GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(
                DesignerSettingItemModels,
                DesignerExclusiveSettingItemModels,
                AdditionalDesignerArgumentsTextBox.Text ?? string.Empty);
            SetEditorText(ComponentCommandLineFormatter.FormatByFlags(command.Trim()));
            _designerNeedsRefresh = false;
        }
        finally
        {
            _updatingDesigner = false;
        }
    }
}
