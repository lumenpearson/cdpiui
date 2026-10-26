using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CDPIUI.Views.Store;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CDPIUI;

public sealed partial class HomePage
{
    private double _largeElementWidth;
    public double LargeElementWidth
    {
        get => _largeElementWidth;
        set
        {
            if (_largeElementWidth != value)
            {
                _largeElementWidth = value;
                OnPropertyChanged();
            }
        }
    }

    private double _smallElementWidth;
    public double SmallElementWidth
    {
        get => _smallElementWidth;
        set
        {
            if (_smallElementWidth != value)
            {
                _smallElementWidth = value;
                OnPropertyChanged();
            }
        }
    }

    private double _currentPageWidth;
    public double CurrentPageWidth
    {
        get => _currentPageWidth;
        set
        {
            if (_currentPageWidth != value)
            {
                _currentPageWidth = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isFirstLayout = true;

    private void HomePage_LayoutUpdated(object sender, object e)
    {
        if (_isFirstLayout)
        {
            _isFirstLayout = !SetupElementsWidth();
        }
        else
        {
            this.LayoutUpdated -= HomePage_LayoutUpdated;
        }
    }

    public static T FindDescendant<T>(DependencyObject element) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is T t)
                return t;

            var result = FindDescendant<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private double CalcGrid(double newWidth)
    {
        double totalWidth = Math.Min(
            newWidth - StoreScrollViewer.Padding.Left - StoreScrollViewer.Padding.Right,
            MaxTotalWidth);

        return StoreGridLayout.CalculateLargeElementWidth(totalWidth, _largeMinW, _largePrefW);
    }

    private bool SetupElementsWidth()
    {
        bool result = true;
        if (_panels.Count <= 0)
            result = false;

        foreach (var (control, cat) in _panels)
        {
            bool isLarge = cat.Type == "basic_category";
            int elementCount = cat.Items.Count;

            var panel = FindDescendant<ItemsWrapGrid>(control);
            if (panel != null && DataContext is HomePage vm)
            {
                panel.ItemWidth = isLarge ? vm.LargeElementWidth : vm.SmallElementWidth;
                if (!isLarge)
                {
                    if (vm.SmallElementWidth * elementCount > CurrentPageWidth)
                        panel.MaximumRowsOrColumns = 2;
                    else
                        panel.MaximumRowsOrColumns = 1;
                }
                else
                {
                    panel.MaximumRowsOrColumns = 1;
                }
            }
            else
            {
                result = false;
            }
        }
        return result;
    }

    private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        double totalWidth = Math.Min(
            e.NewSize.Width - StoreScrollViewer.Padding.Left - StoreScrollViewer.Padding.Right,
            MaxTotalWidth);
        LargeElementWidth = CalcGrid(e.NewSize.Width);

        CurrentPageWidth = totalWidth;
        StoreViewBundles.Width = CurrentPageWidth;

        SmallElementWidth = LargeElementWidth * 2;

        SetupElementsWidth();
    }
}
