using System;
using CDPIUI.Controls.Navigation;
using CDPIUI.ViewModels.ConfigMaker;
using Microsoft.UI.Xaml;

namespace CDPIUI.Controls.CreateConfigHelper;

// The resizable panels (preset files, command reference and the bottom panel) and their tabs.
public sealed partial class ConfigMakerUserControl
{
    private bool _layoutInitialized;
    private bool _updatingLayoutBounds;
    private double _savedEditorWidth = 620;
    private double _savedEditorHeight = 420;

    public bool IsCommandPanelVisible => CommandPanel.Visibility == Visibility.Visible;
    public bool IsBottomPanelVisible => BottomPanel.Visibility == Visibility.Visible;
    public bool IsPresetFilesPanelVisible => PresetFilesPanel.Visibility == Visibility.Visible;

    public void ShowOutputTab()
    {
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(0);
    }

    public void ShowDiagnosticsTab()
    {
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(1);
    }

    public void ShowPresetGroupsTab()
    {
        if (!HasPresetGroups)
        {
            return;
        }
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(BottomSelector.Items.IndexOf(PresetGroupsSelectorItem));
    }

    public void SetPresetFilesPanelVisible(bool visible)
    {
        PresetFilesSizerColumn.Width = ShowPanel(PresetFilesPanel, PresetFilesContentSizer, visible);
        PresetFilesColumn.Width = visible ? GridLength.Auto : new GridLength(0);
        CompletePanelChange();
    }

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
        VerticalSizerColumn.Width = ShowPanel(CommandPanel, VerticalContentSizer, visible);
        CompletePanelChange();
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
        HorizontalSizerRow.Height = ShowPanel(BottomPanel, HorizontalContentSizer, visible);
        CompletePanelChange();
    }

    // Shows or hides a panel with its sizer and returns the length of the sizer column or row.
    private static GridLength ShowPanel(UIElement panel, UIElement sizer, bool visible)
    {
        panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        sizer.Visibility = panel.Visibility;
        return visible ? new GridLength(12) : new GridLength(0);
    }

    private void CompletePanelChange()
    {
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

    private void CloseTreeViewButton_Click(object sender, RoutedEventArgs e) => SetPresetFilesPanelVisible(false);

    private void CloseCommandsViewButton_Click(object sender, RoutedEventArgs e) => SetCommandPanelVisible(false);

    private void CloseCommandsBottomPanelButton_Click(object sender, RoutedEventArgs e) => SetBottomPanelVisible(false);

    private void DiagnosticsStatusButton_Click(object sender, RoutedEventArgs e) =>
        ShowDiagnosticsTab();

    void IConfigMakerView.ShowVariablesTool() => ToolsSelector.SelectIndex(1);

    private void UpdatePresetGroupsTab()
    {
        bool shouldShow = ViewModel.HasPresetGroups;
        bool containsItem = BottomSelector.Items.Contains(PresetGroupsSelectorItem);
        if (shouldShow && !containsItem)
        {
            BottomSelector.Items.Add(PresetGroupsSelectorItem);
        }
        else if (!shouldShow && containsItem)
        {
            if (BottomSelector.SelectedIndex == BottomSelector.Items.IndexOf(PresetGroupsSelectorItem))
            {
                BottomSelector.SelectIndex(0);
            }
            BottomSelector.Items.Remove(PresetGroupsSelectorItem);
        }
    }

    private void UpdateSimpleDesignerTab()
    {
        if (EditorViewSelector == null || SimpleDesignerSelectorItem == null)
        {
            return;
        }
        bool supported = ViewModel.IsSimpleDesignerSupported;
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
        }
    }

    private void EditorViewSelector_SelectionChanged(object sender, AnimatedSelectorBarSelectionChangedEventArgs e) =>
        ViewModel.OnEditorViewChanged(
            leftDesigner: ReferenceEquals(e.OldItem, SimpleDesignerSelectorItem),
            enteredDesigner: ReferenceEquals(e.NewItem, SimpleDesignerSelectorItem));
}
