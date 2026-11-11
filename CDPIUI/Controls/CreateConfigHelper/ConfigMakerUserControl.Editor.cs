using System;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels.ConfigMaker;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TextControlBoxNS;
using Windows.UI;

namespace CDPIUI.Controls.CreateConfigHelper;

// The command text editor: text, cursor, zoom, background and highlighting.
public sealed partial class ConfigMakerUserControl
{
    private readonly DiagnosticHighlightingUpdater _highlightingUpdater = new();

    string IConfigMakerView.EditorText => CommandEditor.Text;

    public void ZoomInButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(5);

    string IConfigMakerView.ReplaceEditorText(string text)
    {
        CommandEditor.Text = text;
        return CommandEditor.Text;
    }

    (int Line, int Column) IConfigMakerView.GetCursorPosition()
    {
        Windows.Foundation.Point cursor = CommandEditor.GetCursorPosition();
        return ((int)cursor.Y, (int)cursor.X);
    }

    void IConfigMakerView.SetCursorPosition(int line, int column, bool focus)
    {
        CommandEditor.SetCursorPosition(line, column);
        if (focus)
        {
            CommandEditor.Focus(FocusState.Programmatic);
        }
    }

    void IConfigMakerView.ClearEditorUndoHistory() => CommandEditor.ClearUndoRedoHistory();

    void IConfigMakerView.RefreshEditorHighlighting() => ApplyEditorTheme();

    private void CommandEditor_TextChanged(TextControlBox sender) => ViewModel.OnEditorTextChanged(sender.Text);

    private void ReplaceButton_Click(object sender, RoutedEventArgs e) =>
        EditorSearchControl.ShowReplace(CommandEditor);

    private void CommandEditor_ZoomChanged(TextControlBox sender, int zoomFactor) => UpdateZoomStatus();

    private void ChangeZoom(int delta)
    {
        CommandEditor.ZoomFactor = delta == 0
            ? 100
            : Math.Clamp(CommandEditor.ZoomFactor + delta, 25, 400);
        UpdateZoomStatus();
    }

    private void UpdateZoomStatus() =>
        ZoomResetButton.Content = $"{CommandEditor.ZoomFactor}%";

    private void DiagnosticsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiagnosticsListView.SelectedItem is ConfigMakerDiagnosticViewModel diagnostic)
        {
            GoToDiagnostic(diagnostic);
        }
    }

    private void DiagnosticsListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DiagnosticsListView.SelectedItem is ConfigMakerDiagnosticViewModel diagnostic)
        {
            GoToDiagnostic(diagnostic);
        }
    }

    private void GoToDiagnostic(ConfigMakerDiagnosticViewModel diagnostic)
    {
        CommandEditor.SetCursorPosition(
            Math.Max(0, diagnostic.Source.Line - 1),
            Math.Max(0, diagnostic.Source.Column - 1));
        CommandEditor.Focus(FocusState.Programmatic);
    }

    private void PresetGroupsTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (PresetGroupsTreeView.SelectedNode?.Content is not ConfigMakerPresetTreeItemViewModel item ||
            item.SourceIndex < 0 ||
            item.SourceLength <= 0)
        {
            return;
        }

        CommandEditor.Focus(FocusState.Programmatic);
        CommandEditor.SetCursorPosition(
            item.SourceLine,
            item.SourceColumn,
            scrollIntoView: true,
            autoClamp: true);
        CommandEditor.SetSelection(item.SourceIndex, item.SourceLength);
        CommandEditor.ScrollLineToCenter(item.SourceLine);
        e.Handled = true;
    }

    private void ApplyEditorBackground()
    {
        EditorPanel.Background = ViewModel.EditorBackground switch
        {
            "Mica" => GetApplicationBrush("LayerFillColorDefaultBrush"),
            "MicaTransparent" => new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            "MicaSmoke" => GetApplicationBrush("SmokeFillColorDefaultBrush"),
            _ => new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
        };
        _highlightingUpdater.Reset();
        ApplyEditorTheme();
    }

    private static Brush GetApplicationBrush(string resourceKey) =>
        Application.Current.Resources.TryGetValue(resourceKey, out object resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    private void ApplyEditorTheme()
    {
        string editorBackground = ViewModel.EditorBackground;
        bool usesForcedDarkBackground = editorBackground is "Black" or "MicaSmoke";
        bool isLight = !usesForcedDarkBackground &&
            (ActualTheme == ElementTheme.Light ||
             (ActualTheme == ElementTheme.Default && ((App)Application.Current).CurrentTheme == ElementTheme.Light));
        _highlightingUpdater.Update(editorBackground, isLight, () => ViewModel.Diagnostics,
            static item => item.Source.Severity == ComponentCommandDiagnosticSeverity.Error,
            static item => item.Source.Severity == ComponentCommandDiagnosticSeverity.Warning,
            static item => item.Source.Token,
            (light, errorTokens, warningTokens) =>
            {
                CommandEditor.SyntaxHighlighting = light
                    ? new LightDefaultHighlighter(errorTokens, warningTokens)
                    : new DarkDefaultHighlighter(errorTokens, warningTokens);
                CommandEditor.Design = light
                    ? TextControlBoxDesigns.DefaultLightDesign
                    : TextControlBoxDesigns.DefaultDarkDesign;
            });
    }
}
