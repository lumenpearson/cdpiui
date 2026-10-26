using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace CDPIUI.Controls.CreateConfigHelper;

// The trees, the lists and the inputs of the panels, and dropping files on the editor.
public sealed partial class ConfigMakerUserControl
{
    private static void FillTree(TreeView treeView, IEnumerable<ConfigMakerTreeNode> nodes)
    {
        treeView.RootNodes.Clear();
        foreach (ConfigMakerTreeNode node in nodes)
        {
            treeView.RootNodes.Add(CreateTreeViewNode(node));
        }
    }

    private static TreeViewNode CreateTreeViewNode(ConfigMakerTreeNode node)
    {
        TreeViewNode result = new()
        {
            Content = node.Content,
            IsExpanded = node.IsExpanded,
        };
        foreach (ConfigMakerTreeNode child in node.Children)
        {
            result.Children.Add(CreateTreeViewNode(child));
        }
        return result;
    }

    private static ConfigMakerPresetFileInfo GetPresetFileFromMenuSender(object sender)
    {
        if (sender is not FrameworkElement element)
        {
            return null;
        }
        if (element.Tag is ConfigMakerPresetFileInfo taggedFile)
        {
            return taggedFile;
        }
        if (element.DataContext is TreeViewNode node &&
            node.Content is ConfigMakerPresetFileTreeItem nodeItem)
        {
            return nodeItem.File;
        }
        return (element.DataContext as ConfigMakerPresetFileTreeItem)?.File;
    }

    private static ConfigMakerVariableDefinition GetVariableFromSender(object sender) =>
        (sender as FrameworkElement)?.Tag as ConfigMakerVariableDefinition;

    private async void ReplacePresetFileItem_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ReplacePresetFileAsync(GetPresetFileFromMenuSender(sender));

    private async void OpenPresetFileItem_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.OpenPresetFileAsync(GetPresetFileFromMenuSender(sender));

    private void ShowPresetFileInFolderItem_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowPresetFileInFolder(GetPresetFileFromMenuSender(sender));

    private async void RemovePresetFileItem_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.RemovePresetFileAsync(GetPresetFileFromMenuSender(sender));

    private void PresetFilesTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (PresetFilesTreeView.SelectedNode?.Content is ConfigMakerPresetFileTreeItem item)
        {
            ViewModel.InsertPresetFileReference(item.File);
        }
    }

    private async void EditVariableButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.EditVariableAsync(GetVariableFromSender(sender));

    private async void RemoveVariableButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.RemoveVariableAsync(GetVariableFromSender(sender));

    private void PresetVariablesListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        ViewModel.InsertVariableReference(PresetVariablesListView.SelectedItem as ConfigMakerVariableDefinition);

    private void CommandSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.CommandSearchText = sender.Text;
        }
    }

    private void CommandListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectCommand(CommandListView.SelectedItem as ConfigMakerCommandOptionViewModel);
        Grid.SetRowSpan(CommandListView, ViewModel.HasSelectedCommand ? 1 : 2);
    }

    private void AdditionalDesignerArgumentsTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ViewModel.OnAdditionalDesignerArgumentsChanged(AdditionalDesignerArgumentsTextBox.Text);

    private void EditorPanel_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = _localizer.GetLocalizedString("ConfigMakerAddPresetFileDialogTitle");
        e.DragUIOverride.IsCaptionVisible = true;
        e.Handled = true;
    }

    private async void EditorPanel_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        try
        {
            IReadOnlyList<IStorageItem> droppedItems = await e.DataView.GetStorageItemsAsync();
            ViewModel.AddPresetFiles(droppedItems
                .OfType<StorageFile>()
                .Select(file => file.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path)));
        }
        catch (Exception exception)
        {
            ViewModel.ReportHandlerError(exception);
        }
        finally
        {
            e.Handled = true;
        }
    }
}
