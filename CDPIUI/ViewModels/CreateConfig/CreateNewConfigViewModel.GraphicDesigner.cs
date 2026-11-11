using System;

namespace CDPIUI.ViewModels.CreateConfig;

// The graphic designer for GoodbyeDPI, SpoofDPI and NoDPI.
public sealed partial class CreateNewConfigViewModel
{
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
        _graphicDesigner.Load(id);
    }

    private void HandleGraphicDesignerTextValueChanged(Tuple<string, string> tuple) => _graphicDesigner.ChangeText(tuple);

    private void HandleGraphicDesignerBoolValueToggled(Tuple<string, bool> tuple) => _graphicDesigner.ToggleBool(tuple);

    private void HandleGraphicDesignerSelectedGuidChanged(Tuple<string, string> tuple) => _graphicDesigner.SelectGuid(tuple);

    private void ConvertStringToDesignerLikeSettings()
    {
        _view.EditorText = _view.EditorText.Replace("\n", " ");
        _graphicDesigner.Parse(_view.EditorText);
    }

    private void ConvertDesignerLikeSettingsToString()
    {
        if (SelectedComponent != null && !IsDesignerSupported)
        {
            return;
        }

        _view.EditorText = _graphicDesigner.Build();
        _view.EditorText = _view.EditorText.Replace("\n", " ");
    }
}
