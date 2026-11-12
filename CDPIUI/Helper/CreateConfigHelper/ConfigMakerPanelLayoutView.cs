#nullable enable

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Helper.CreateConfigHelper;

// Lazy getters also support layout events raised while the control is loading its XAML.
internal sealed class ConfigMakerPanelLayoutView : IConfigMakerPanelLayoutView
{
    private readonly Func<FrameworkElement> _root, _files, _commands, _bottom, _workspace, _editor;
    private readonly Func<ColumnDefinition> _filesSizer, _commandSizer;
    private readonly Func<RowDefinition> _bottomSizer;

    public ConfigMakerPanelLayoutView(Func<FrameworkElement> root, Func<FrameworkElement> files,
        Func<FrameworkElement> commands, Func<FrameworkElement> bottom, Func<FrameworkElement> workspace,
        Func<FrameworkElement> editor, Func<ColumnDefinition> filesSizer,
        Func<ColumnDefinition> commandSizer, Func<RowDefinition> bottomSizer)
    {
        _root = root; _files = files; _commands = commands; _bottom = bottom;
        _workspace = workspace; _editor = editor;
        _filesSizer = filesSizer; _commandSizer = commandSizer; _bottomSizer = bottomSizer;
    }

    public double RootWidth => _root().ActualWidth;
    public double RootHeight => _root().ActualHeight;
    public bool FilesVisible => _files().Visibility == Visibility.Visible;
    public bool CommandsVisible => _commands().Visibility == Visibility.Visible;
    public bool BottomVisible => _bottom().Visibility == Visibility.Visible;
    public double FilesMinWidth => _files().MinWidth;
    public double FilesMaxWidth { get => _files().MaxWidth; set => _files().MaxWidth = value; }
    public double FilesWidth { get => _files().Width; set => _files().Width = value; }
    public double FilesSizerWidth => _filesSizer().Width.Value;
    public double CommandSizerWidth => _commandSizer().Width.Value;
    public double BottomSizerHeight => _bottomSizer().Height.Value;
    public double EditorMaxWidth { set => _workspace().MaxWidth = value; }
    public double EditorWidth { set => _workspace().Width = value; }
    public double EditorMaxHeight { set => _editor().MaxHeight = value; }
    public double EditorHeight { get => _editor().Height; set => _editor().Height = value; }
}
