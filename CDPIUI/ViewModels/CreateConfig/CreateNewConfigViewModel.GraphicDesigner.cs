using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

// The graphic designer for GoodbyeDPI, SpoofDPI and NoDPI.
public sealed partial class CreateNewConfigViewModel
{
    private string _nowConfigLoadedId = string.Empty;

    /// <summary>Call when the user switched between the text editor and the designer.</summary>
    public void OnEditorTabChanged() => HandleSelectionChange();

    private void HandleSelectionChange(bool showDefaultOnly = false)
    {
        _view.ApplyEditorBackground();
        if (_view.IsTextEditorTabSelected || showDefaultOnly)
        {
            IsTextEditorVisible = true;
            IsDesignerVisible = false;
            if (!showDefaultOnly)
                ConvertDesignerLikeSettingsToString();
        }
        else
        {
            IsTextEditorVisible = false;
            IsDesignerVisible = true;
            LoadDesigner();
            ConvertStringToDesignerLikeSettings();
            _view.CloseSearch();
        }
    }

    private void LoadDesigner()
    {
        if (SelectedComponent == null)
            return;
        string id = SelectedComponent.Id;
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
        bool value = tuple.Item2;

        var item = DesignerSettingItemModels.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            item.IsChecked = value;
        }
        else
        {
            item = DesignerExclusiveSettingItemModels.FirstOrDefault(x => x.Items.FirstOrDefault(y => y.Guid == guid) != null)?.Items.FirstOrDefault(y => y.Guid == guid);
            if (item != null)
            {
                item.IsChecked = value;
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
        _view.EditorText = _view.EditorText.Replace("\n", " ");
        AdditionalSettings = GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(DesignerSettingItemModels, DesignerExclusiveSettingItemModels, _view.EditorText);
    }

    private void ConvertDesignerLikeSettingsToString()
    {
        if (SelectedComponent != null && !IsDesignerSupported)
        {
            return;
        }

        _view.EditorText = GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(DesignerSettingItemModels, DesignerExclusiveSettingItemModels, AdditionalSettings);
        _view.EditorText = _view.EditorText.Replace("\n", " ");
    }
}
