using System;
using Microsoft.UI.Xaml;

namespace CDPIUI.Controls.CreateConfigHelper;

// The resizable panels: preset files, command reference and the bottom panel.
public sealed partial class ConfigMakerUserControl
{
    public void SetCommandPanelVisible(bool visible)
    {
        if (visible == IsCommandPanelVisible)
        {
            return;
        }

        if (!visible)
        {
            _savedEditorWidth = EditorWorkspace.ActualWidth;
        }

        CommandPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        VerticalContentSizer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        VerticalSizerColumn.Width = visible ? new GridLength(12) : new GridLength(0);
        _layoutInitialized = false;
        UpdateLayoutBounds();
        PanelStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBottomPanelVisible(bool visible)
    {
        if (visible == IsBottomPanelVisible)
        {
            return;
        }

        if (!visible)
        {
            _savedEditorHeight = EditorPanel.ActualHeight;
        }

        BottomPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        HorizontalContentSizer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        HorizontalSizerRow.Height = visible ? new GridLength(12) : new GridLength(0);
        _layoutInitialized = false;
        UpdateLayoutBounds();
        PanelStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateLayoutBounds();

    private void ResizablePanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_updatingLayoutBounds)
        {
            UpdateLayoutBounds();
        }
    }

    private void UpdateLayoutBounds()
    {
        if (_updatingLayoutBounds || RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
        {
            return;
        }

        _updatingLayoutBounds = true;
        try
        {
            const double editorMinWidth = 360;
            const double commandMinWidth = 340;
            const double editorMinHeight = 180;
            const double bottomMinHeight = 110;

            double filesWidth = 0;
            if (IsPresetFilesPanelVisible)
            {
                double maximumFilesWidth = Math.Max(
                    150,
                    RootGrid.ActualWidth - editorMinWidth -
                    (IsCommandPanelVisible ? commandMinWidth + VerticalSizerColumn.Width.Value : 0) -
                    PresetFilesSizerColumn.Width.Value);
                PresetFilesPanel.MaxWidth = Math.Max(
                    PresetFilesPanel.MinWidth,
                    Math.Min(RootGrid.ActualWidth * 0.45, maximumFilesWidth));
                PresetFilesPanel.Width = Math.Clamp(
                    PresetFilesPanel.Width,
                    PresetFilesPanel.MinWidth,
                    PresetFilesPanel.MaxWidth);
                filesWidth = PresetFilesPanel.Width + PresetFilesSizerColumn.Width.Value;
            }

            double availableMainWidth = Math.Max(editorMinWidth, RootGrid.ActualWidth - filesWidth);

            if (IsCommandPanelVisible)
            {
                double maxEditorWidth = Math.Max(
                    editorMinWidth,
                    availableMainWidth - commandMinWidth - VerticalSizerColumn.Width.Value);
                EditorWorkspace.MaxWidth = maxEditorWidth;
                double requestedWidth = _layoutInitialized
                    ? EditorWorkspace.Width
                    : Math.Min(_savedEditorWidth, maxEditorWidth);
                EditorWorkspace.Width = maxEditorWidth;
            }
            else
            {
                EditorWorkspace.MaxWidth = availableMainWidth;
                EditorWorkspace.Width = availableMainWidth;
            }

            double availableMainHeight = Math.Max(editorMinHeight, RootGrid.ActualHeight);

            if (IsBottomPanelVisible)
            {
                double maxEditorHeight = Math.Max(
                    editorMinHeight,
                    availableMainHeight - bottomMinHeight - HorizontalSizerRow.Height.Value);
                EditorPanel.MaxHeight = maxEditorHeight;
                double requestedHeight = _layoutInitialized
                    ? EditorPanel.Height
                    : Math.Min(_savedEditorHeight, maxEditorHeight);
                EditorPanel.Height = Math.Clamp(requestedHeight, editorMinHeight, maxEditorHeight);
            }
            else
            {
                EditorPanel.MaxHeight = availableMainHeight;
                EditorPanel.Height = availableMainHeight;
            }

            _layoutInitialized = true;
        }
        finally
        {
            _updatingLayoutBounds = false;
        }
    }

    private void CloseTreeViewButton_Click(object sender, RoutedEventArgs e)
    {
        SetPresetFilesPanelVisible(false);
    }

    private void CloseCommandsViewButton_Click(object sender, RoutedEventArgs e)
    {
        SetCommandPanelVisible(false);
    }

    private void CloseCommandsBottomPanelButton_Click(object sender, RoutedEventArgs e)
    {
        SetBottomPanelVisible(false);
    }
}
