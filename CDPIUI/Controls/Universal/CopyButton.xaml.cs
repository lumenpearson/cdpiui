using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CDPIUI.Helper;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Universal;

public sealed partial class CopyButton : Button
{
    public static readonly DependencyProperty CopiedMessageProperty =
        DependencyProperty.Register("CopiedMessage", typeof(string), typeof(CopyButton), new PropertyMetadata(null));

    public string CopiedMessage
    {
        get { return (string)GetValue(CopiedMessageProperty) ?? EmergencyLocalization.Get("CopyButton.CopiedMessage"); }
        set { SetValue(CopiedMessageProperty, value); }
    }

    public CopyButton()
    {
        this.DefaultStyleKey = typeof(CopyButton);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetTemplateChild("CopyToClipboardSuccessAnimation") is Storyboard _storyBoard)
        {
            _storyBoard.Begin();
            UIHelper.AnnounceActionForAccessibility(this, CopiedMessage, "CopiedToClipboardActivityId");
        }
    }

    protected override void OnApplyTemplate()
    {
        Click -= CopyButton_Click;
        base.OnApplyTemplate();
        // The normal XAML UID or the error dialog can supply more specific labels.
        // Defaults must also work when the regular localizer is unavailable.
        if (ReadLocalValue(Microsoft.UI.Xaml.Automation.AutomationProperties.NameProperty) == DependencyProperty.UnsetValue)
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, EmergencyLocalization.Get("CopyButton.ToolTipService.ToolTip"));
        if (ReadLocalValue(ToolTipService.ToolTipProperty) == DependencyProperty.UnsetValue)
            ToolTipService.SetToolTip(this, EmergencyLocalization.Get("CopyButton.ToolTipService.ToolTip"));
        Click += CopyButton_Click;
    }
}
