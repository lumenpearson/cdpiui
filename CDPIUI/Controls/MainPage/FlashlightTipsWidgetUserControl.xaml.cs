using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CDPIUI.Core;
using CDPIUI.Helper.UserExperience;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.MainPage;

public sealed partial class FlashlightTipsWidgetUserControl : UserControl
{

    private List<string> _tips = [];

    private int _currentIndex = 0;
    private bool _isAnimating = false;

    private int _targetIndex = 0;
    private double _slideInFromX = 0;

    private ILocalizer _localizer = Localizer.Get();

    public FlashlightTipsWidgetUserControl()
    {
        InitializeComponent();

        this.Visibility = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "showFlashlightWidget") ? Visibility.Visible : Visibility.Collapsed;

        LoadTips();

        ConnectHandlers();
    }

    public void ConnectHandlers()
    {
        // Also called by MainPage after the constructor did it: remove first, or every
        // animation step ran twice.
        SlideOutStoryboard.Completed -= SlideOutStoryboard_Completed;
        SlideInStoryboard.Completed -= SideInStoryboard_Completed;
        this.Unloaded -= FlashlightTipsWidgetUserControl_Unloaded;
        SlideOutStoryboard.Completed += SlideOutStoryboard_Completed;
        SlideInStoryboard.Completed += SideInStoryboard_Completed;
        this.Unloaded += FlashlightTipsWidgetUserControl_Unloaded;
    }

    private void FlashlightTipsWidgetUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        SlideOutStoryboard.Completed -= SlideOutStoryboard_Completed;
        SlideInStoryboard.Completed -= SideInStoryboard_Completed;
        this.Unloaded -= FlashlightTipsWidgetUserControl_Unloaded;
    }

    private void LoadTips()
    {
        if (!FlashlightHelper.LoadFlashlightTips(_tips))
        {
            _tips.Add(_localizer.GetLocalizedString("FlashlightBad"));
            _tips.Add(":(");
            TipTitleTextBlock.Text = _localizer.GetLocalizedString("FlashlightBadTitle");
        }
        else
        {
            TipTitleTextBlock.Text = _localizer.GetLocalizedString("/Flashlight/AreYouKnow");
        }

        // The first tip is shown directly: starting from Tips.Count skipped tip 0, and with a
        // single tip MoveNext did nothing and the widget stayed empty.
        _currentIndex = 0;
        TipDescriptionTextBlock.Text = _tips.Count > 0 ? _tips[0] : string.Empty;
    }

    private void PreviousTipButton_Click(object sender, RoutedEventArgs e)
    {
        MovePrevious();
    }

    private void NextTipButton_Click(object sender, RoutedEventArgs e)
    {
        MoveNext();
    }

    private void HideFlashlightWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("APPEARANCE", "showFlashlightWidget", false);
        this.Visibility = Visibility.Collapsed;
        // TODO: Add goodbye message.
    }

    public void MoveNext()
    {
        if (_isAnimating || _tips.Count <= 1)
            return;

        _targetIndex = (_currentIndex + 1) % _tips.Count;
        StartTransition(slideOutToX: -200, slideInFromX: 200);
    }

    public void MovePrevious()
    {
        if (_isAnimating || _tips.Count <= 1)
            return;

        _targetIndex = (_currentIndex - 1 + _tips.Count) % _tips.Count;
        StartTransition(slideOutToX: 200, slideInFromX: -200);
    }


    private void StartTransition(double slideOutToX, double slideInFromX)
    {
        _isAnimating = true;
        _slideInFromX = slideInFromX;

        SlideOutAnimation.To = slideOutToX;

        SlideOutStoryboard.Begin();
    }

    private void SlideOutStoryboard_Completed(object sender, object e)
    {
        _currentIndex = _targetIndex;
        TipDescriptionTextBlock.Text = _tips[_currentIndex];

        TextTransform.X = _slideInFromX;

        SlideInStoryboard.Begin();
    }

    private void SideInStoryboard_Completed(Object sender, object e)
    {
        _isAnimating = false;
    }
}
