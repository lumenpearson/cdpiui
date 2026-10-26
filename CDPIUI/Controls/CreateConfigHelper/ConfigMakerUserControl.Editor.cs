using System;
using System.Linq;
using CDPIUI.Core;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TextControlBoxNS;
using Windows.UI;

namespace CDPIUI.Controls.CreateConfigHelper;

// The command text editor: text sync, cursor insertion, zoom, background and highlighting.
public sealed partial class ConfigMakerUserControl
{
    public void FormatCommand()
    {
        if (IsEditorReadOnly)
        {
            return;
        }
        SetEditorText(ComponentCommandLineFormatter.FormatByFlags(CommandEditor.Text));
        CommandEditor.SetCursorPosition(0, 0);
    }

    public void ZoomInButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(5);

    private void CommandEditor_TextChanged(TextControlBox sender)
    {
        if (_updatingEditor)
        {
            return;
        }
        if (IsEditorReadOnly)
        {
            SetEditorText(CommandText, updateProperty: false);
            return;
        }

        _updatingEditor = true;
        CommandText = sender.Text ?? string.Empty;
        _updatingEditor = false;
        _presetDocument.CommandText = CommandText;
        _designerNeedsRefresh = true;
        RebuildPresetStructure();
        CommandTextChanged?.Invoke(CommandText);
        ScheduleDiagnostics();
    }

    private void SetEditorText(string text, bool updateProperty = true)
    {
        _updatingEditor = true;
        CommandEditor.Text = text ?? string.Empty;
        if (updateProperty)
        {
            CommandText = CommandEditor.Text;
        }
        _presetDocument.CommandText = CommandEditor.Text;
        _designerNeedsRefresh = true;
        _updatingEditor = false;
        RebuildPresetStructure();
        RebuildDiagnostics();
        CommandTextChanged?.Invoke(CommandEditor.Text);
    }

    private void InsertAtCursor(string command)
    {
        Windows.Foundation.Point cursor = CommandEditor.GetCursorPosition();
        string result = ConfigMakerCommandText.InsertOnOwnLine(
            CommandEditor.Text,
            (int)cursor.Y,
            (int)cursor.X,
            command,
            out int newCursorIndex);

        SetEditorText(result.Replace("\n", Environment.NewLine));
        (int line, int column) = ConfigMakerCommandText.GetLineAndColumn(result, newCursorIndex);
        CommandEditor.SetCursorPosition(line, column);
        CommandEditor.Focus(FocusState.Programmatic);
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) =>
        EditorSearchControl.ShowSearch(CommandEditor);

    private void ReplaceButton_Click(object sender, RoutedEventArgs e) =>
        EditorSearchControl.ShowReplace(CommandEditor);

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(-5);

    private void ZoomResetButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(0);

    private void CommandEditor_ZoomChanged(TextControlBox sender, int zoomFactor)
    {
        UpdateZoomStatus();
    }

    private void ChangeZoom(int delta)
    {
        CommandEditor.ZoomFactor = delta == 0
            ? 100
            : Math.Clamp(CommandEditor.ZoomFactor + delta, 25, 400);
        UpdateZoomStatus();
    }

    private void UpdateZoomStatus() =>
        ZoomResetButton.Content = $"{CommandEditor.ZoomFactor}%";

    private void LoadEditorBackground()
    {
        string savedBackground = SettingsManager.Instance.GetValue<string>(
            "APPEARANCE",
            "configEditorBackground");
        _editorBackground = savedBackground switch
        {
            "Mica" => "Mica",
            "MicaTransparent" => "MicaTransparent",
            "MicaSmoke" => "MicaSmoke",
            _ => "Black",
        };
        DefaultBackgroundItem.IsChecked = _editorBackground == "Black";
        MicaBackgroundItem.IsChecked = _editorBackground == "Mica";
        MicaAltBackgroundItem.IsChecked = _editorBackground == "MicaTransparent";
        MicaSmokeBackgroundItem.IsChecked = _editorBackground == "MicaSmoke";
        ApplyEditorBackground();
    }

    private void EditorBackgroundItem_Click(object sender, RoutedEventArgs e)
    {
        _editorBackground = sender switch
        {
            RadioMenuFlyoutItem item when ReferenceEquals(item, MicaBackgroundItem) => "Mica",
            RadioMenuFlyoutItem item when ReferenceEquals(item, MicaAltBackgroundItem) => "MicaTransparent",
            RadioMenuFlyoutItem item when ReferenceEquals(item, MicaSmokeBackgroundItem) => "MicaSmoke",
            _ => "Black",
        };
        SettingsManager.Instance.SetValue(
            "APPEARANCE",
            "configEditorBackground",
            _editorBackground);
        ApplyEditorBackground();
    }

    private void ApplyEditorBackground()
    {
        EditorPanel.Background = _editorBackground switch
        {
            "Mica" => GetApplicationBrush("LayerFillColorDefaultBrush"),
            "MicaTransparent" => new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            "MicaSmoke" => GetApplicationBrush("SmokeFillColorDefaultBrush"),
            _ => new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
        };
        _highlightSignature = string.Empty;
        ApplyEditorTheme();
    }

    private static Brush GetApplicationBrush(string resourceKey) =>
        Application.Current.Resources.TryGetValue(resourceKey, out object resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    private void ApplyEditorTheme()
    {
        bool usesForcedDarkBackground = _editorBackground is "Black" or "MicaSmoke";
        bool isLight = !usesForcedDarkBackground &&
            (ActualTheme == ElementTheme.Light ||
             (ActualTheme == ElementTheme.Default && ((App)Application.Current).CurrentTheme == ElementTheme.Light));
        string[] errorTokens = Diagnostics
            .Where(item => item.Source.Severity == ComponentCommandDiagnosticSeverity.Error)
            .Select(item => item.Source.Token)
            .ToArray();
        string[] warningTokens = Diagnostics
            .Where(item => item.Source.Severity == ComponentCommandDiagnosticSeverity.Warning)
            .Select(item => item.Source.Token)
            .ToArray();
        string newSignature = $"{_editorBackground}|{isLight}|{string.Join(',', errorTokens)}|{string.Join(',', warningTokens)}";
        if (string.Equals(_highlightSignature, newSignature, StringComparison.Ordinal))
        {
            return;
        }

        _highlightSignature = newSignature;
        CommandEditor.SyntaxHighlighting = isLight
            ? new LightDefaultHighlighter(errorTokens, warningTokens)
            : new DarkDefaultHighlighter(errorTokens, warningTokens);
        CommandEditor.Design = isLight
            ? TextControlBoxDesigns.DefaultLightDesign
            : TextControlBoxDesigns.DefaultDarkDesign;
    }
}
