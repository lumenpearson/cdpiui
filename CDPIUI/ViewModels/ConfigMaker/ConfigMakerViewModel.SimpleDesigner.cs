using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The simple graphic designer for GoodbyeDPI, SpoofDPI and NoDPI.
public sealed partial class ConfigMakerViewModel
{
    private bool _updatingDesigner;
    private bool _designerNeedsRefresh = true;
    private string _additionalDesignerArguments = string.Empty;

    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels { get; } = [];
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels { get; } = [];
    public ICommand DesignerTextValueChangedCommand { get; }
    public ICommand DesignerBoolValueToggledCommand { get; }
    public ICommand DesignerSelectedGuidChangedCommand { get; }

    public bool IsSimpleDesignerSupported => IsSimpleDesignerComponent(ComponentId);

    public string AdditionalDesignerArguments
    {
        get => _additionalDesignerArguments;
        private set => SetProperty(ref _additionalDesignerArguments, value ?? string.Empty);
    }

    /// <summary>Called by the view when the text of the additional arguments box changed.</summary>
    public void OnAdditionalDesignerArgumentsChanged(string text)
    {
        // The box already shows the text: store it without raising a change back to it.
        _additionalDesignerArguments = text ?? string.Empty;
        ApplySimpleDesignerToCommand();
    }

    /// <summary>Called by the view when the editor switches between the code editor and the designer.</summary>
    public void OnEditorViewChanged(bool leftDesigner, bool enteredDesigner)
    {
        if (leftDesigner)
        {
            ApplySimpleDesignerToCommand();
        }
        if (enteredDesigner && _designerNeedsRefresh)
        {
            LoadSimpleDesigner();
        }
    }

    private static bool IsSimpleDesignerComponent(string componentId) =>
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.GoodbyeDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.SpoofDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.NoDPI], StringComparison.OrdinalIgnoreCase);

    // The view shows or hides the designer tab when IsSimpleDesignerSupported is raised.
    private void UpdateSimpleDesignerAvailability()
    {
        OnPropertyChanged(nameof(IsSimpleDesignerSupported));
        if (!IsSimpleDesignerSupported)
        {
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
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
            AdditionalDesignerArguments = ComponentCommandLineFormatter.ToSingleLine(_view.EditorText);
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            if (!TryLoadDesignerModels())
            {
                return;
            }
            AttachSimpleDesignerCommands();
            AdditionalDesignerArguments =
                GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(
                    DesignerSettingItemModels,
                    DesignerExclusiveSettingItemModels,
                    ComponentCommandLineFormatter.ToSingleLine(_view.EditorText));
            _designerNeedsRefresh = false;
        }
        catch (Exception exception)
        {
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            Core.Basic.Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
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
                _additionalDesignerArguments);
            SetEditorText(ComponentCommandLineFormatter.FormatByFlags(command.Trim()));
            _designerNeedsRefresh = false;
        }
        finally
        {
            _updatingDesigner = false;
        }
    }
}
