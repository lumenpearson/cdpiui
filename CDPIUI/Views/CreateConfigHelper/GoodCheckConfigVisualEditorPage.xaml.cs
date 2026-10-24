using System;
using System.Diagnostics;
using CDPIUI.Controls.Default;
using CDPIUI.ViewModels.CreateConfig;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace CDPIUI.Views.CreateConfigHelper;

public sealed partial class GoodCheckConfigVisualEditorPage : TemplatePage
{
    private DragItem _draggedItem;
    private string _dragSource;

    public GoodCheckConfigVisualEditorPage()
    {
        InitializeComponent();

        this.Loaded += GoodCheckConfigVisualEditorPage_Loaded;
    }

    public GoodCheckConfigVisualEditorViewModel ViewModel { get; } = new();

    public void CalcWidth()
    {
        LeftGrid.MaxWidth = RootPage.ActualWidth - 220;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            ViewModel.SetComponent(Parameter.Get("componentId"));
        }
    }

    private void GoodCheckConfigVisualEditorPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (Parameter != null)
        {
            ViewModel.LoadStrategies(Parameter.Get("dragList"));
        }

        this.Loaded -= GoodCheckConfigVisualEditorPage_Loaded;
    }

    private void RootPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CalcWidth();
    }

    private void LeftListView_Loaded(object sender, RoutedEventArgs e)
    {
        CalcWidth();
        LeftGrid.Width = (RootPage.ActualWidth - 40) / 2;
    }

    private void LeftListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        _draggedItem = e.Items[0] as DragItem;
        _dragSource = "Left";

        if (_draggedItem != null)
        {
            try
            {
                Debug.WriteLine(_draggedItem.Id.ToString());
                e.Data.SetText(_draggedItem.Args.ToString());
                e.Data.Properties.Title = _draggedItem.Id.ToString();
                e.Data.RequestedOperation = DataPackageOperation.Copy;
            }
            catch
            {
                _draggedItem = null;
                _dragSource = null;
            }
        }
    }

    private void RightListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        _draggedItem = e.Items[0] as DragItem;
        _dragSource = "Right";

        if (_draggedItem != null)
        {
            try
            {
                e.Data.SetText(_draggedItem.Args.ToString());
                e.Data.Properties.Title = _draggedItem.Id.ToString();
                e.Data.RequestedOperation = DataPackageOperation.Move;
            }
            catch
            {
                _draggedItem = null;
                _dragSource = null;
            }
        }
    }

    private void RightListView_DragOver(object sender, DragEventArgs e)
    {
        if (_dragSource == "Left")
        {
            e.AcceptedOperation = ViewModel.CanAddToRight ? DataPackageOperation.Copy : DataPackageOperation.None;
        }
        else if (_dragSource == "Right")
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private void LeftListView_DragOver(object sender, DragEventArgs e)
    {
        if (_dragSource == "Right")
            e.AcceptedOperation = DataPackageOperation.Move;
        else
            e.AcceptedOperation = DataPackageOperation.None;
    }

    private void RightListView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                string idText = e.DataView.Properties.Title;

                if (!ViewModel.CanAddToRight)
                {
                    return;
                }

                if (Guid.TryParse(idText, out Guid parsedId) && _dragSource == "Left")
                {
                    Debug.WriteLine(idText);
                    ViewModel.AddToRight(parsedId);
                }
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(GoodCheckConfigVisualEditorPage), exception.ToString());
        }
        finally
        {
            _draggedItem = null;
            _dragSource = null;
        }
    }

    private void RightListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        ViewModel.CheckRightItemsSort();
    }

    private void LeftListView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                string idText = e.DataView.Properties.Title;

                if (Guid.TryParse(idText, out Guid parsedId) && _dragSource == "Right")
                {
                    ViewModel.RemoveFromRight(parsedId);
                }
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(GoodCheckConfigVisualEditorPage), exception.ToString());
        }
        finally
        {
            _draggedItem = null;
            _dragSource = null;
        }
    }

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(CreateNewConfigPage), ViewModel.CreateEditorParameter(), new DrillInNavigationTransitionInfo());
    }
}
