using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The view-facing contract of the simple graphic designer.
public sealed partial class ConfigMakerViewModel
{
    private readonly ConfigMakerSimpleDesigner _simpleDesigner;

    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels =>
        _simpleDesigner.DesignerSettingItemModels;
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels =>
        _simpleDesigner.DesignerExclusiveSettingItemModels;
    public ICommand DesignerTextValueChangedCommand { get; }
    public ICommand DesignerBoolValueToggledCommand { get; }
    public ICommand DesignerSelectedGuidChangedCommand { get; }
    public bool IsSimpleDesignerSupported => _simpleDesigner.IsSimpleDesignerSupported;
    public string AdditionalDesignerArguments => _simpleDesigner.AdditionalDesignerArguments;

    /// <summary>Called by the view when the text of the additional arguments box changed.</summary>
    public void OnAdditionalDesignerArgumentsChanged(string text) =>
        _simpleDesigner.OnAdditionalDesignerArgumentsChanged(text);

    /// <summary>Called by the view when the editor switches between code and designer.</summary>
    public void OnEditorViewChanged(bool leftDesigner, bool enteredDesigner) =>
        _simpleDesigner.OnEditorViewChanged(leftDesigner, enteredDesigner);

    private void UpdateSimpleDesignerAvailability() => _simpleDesigner.UpdateAvailability();

    private void ShowDesignerNotice(ConfigMakerDesignerNotice notice, Exception exception)
    {
        if (notice == ConfigMakerDesignerNotice.MissingAnnotation)
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerSimpleDesignerAnnotationMissingMessage"),
                InfoBarSeverity.Warning);
            return;
        }
        Core.Basic.Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        ShowEditorMessage(
            string.Format(_localizer.GetLocalizedString("ConfigMakerDesignerLoadFailedMessage"), exception.Message),
            InfoBarSeverity.Warning);
    }
}
