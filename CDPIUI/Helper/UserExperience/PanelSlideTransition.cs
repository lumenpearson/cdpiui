using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace CDPIUI.Helper.UserExperience;

/// <summary>
/// Slides one panel out and another one in, with a fade, like the pages of a wizard.
/// </summary>
internal static class PanelSlideTransition
{
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(320);

    /// <param name="oldPanel">The panel to hide; can be null.</param>
    /// <param name="newPanel">The panel to show.</param>
    /// <param name="width">The distance the panels move.</param>
    /// <param name="slideToLeft">True to move forward (the new panel comes from the right).</param>
    internal static void Run(UIElement oldPanel, UIElement newPanel, double width, bool slideToLeft)
    {
        EnsureTranslateTransform(oldPanel);
        EnsureTranslateTransform(newPanel);

        var duration = TransitionDuration;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        newPanel.Opacity = 0;
        newPanel.Visibility = Visibility.Visible;
        var newTT = GetTranslateTransform(newPanel);
        newTT.X = slideToLeft ? width : -width;

        var outSb = new Storyboard();
        if (oldPanel != null)
        {
            var outAnim = new DoubleAnimation
            {
                From = 0,
                To = slideToLeft ? -width : width,
                Duration = new Duration(duration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(outAnim, oldPanel);
            Storyboard.SetTargetProperty(outAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");
            outSb.Children.Add(outAnim);

            var outFade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(duration)
            };
            Storyboard.SetTarget(outFade, oldPanel);
            Storyboard.SetTargetProperty(outFade, "Opacity");
            outSb.Children.Add(outFade);
        }

        var inSb = new Storyboard();

        var inAnim = new DoubleAnimation
        {
            From = newTT.X,
            To = 0,
            Duration = new Duration(duration),
            EasingFunction = ease
        };
        Storyboard.SetTarget(inAnim, newPanel);
        Storyboard.SetTargetProperty(inAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");
        inSb.Children.Add(inAnim);

        var inFade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(duration)
        };
        Storyboard.SetTarget(inFade, newPanel);
        Storyboard.SetTargetProperty(inFade, "Opacity");
        inSb.Children.Add(inFade);

        if (oldPanel != null)
        {
            outSb.Completed += (s, e) =>
            {
                oldPanel.Visibility = Visibility.Collapsed;
                var tt = GetTranslateTransform(oldPanel);
                tt.X = 0;
                oldPanel.Opacity = 1;
            };
        }

        outSb.Begin();
        inSb.Begin();
    }

    private static void EnsureTranslateTransform(UIElement el)
    {
        if (el == null)
            return;
        if (el.RenderTransform is TranslateTransform)
            return;
        el.RenderTransform = new TranslateTransform();
        el.RenderTransformOrigin = new Windows.Foundation.Point(0, 0);
    }

    private static TranslateTransform GetTranslateTransform(UIElement el)
    {
        if (el?.RenderTransform is TranslateTransform tt)
            return tt;

        var t = new TranslateTransform();
        if (el != null)
            el.RenderTransform = t;
        return t;
    }
}
