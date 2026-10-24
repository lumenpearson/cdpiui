using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CDPIUI.Core;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.LScript;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Core;

namespace CDPIUI.Helper;

public static partial class UIHelper
{
    static UIHelper()
    {
    }

    public static IEnumerable<T> GetDescendantsOfType<T>(this DependencyObject start) where T : DependencyObject
    {
        return start.GetDescendants().OfType<T>();
    }

    public static IEnumerable<DependencyObject> GetDescendants(this DependencyObject start)
    {
        var queue = new Queue<DependencyObject>();
        var count1 = VisualTreeHelper.GetChildrenCount(start);

        for (int i = 0; i < count1; i++)
        {
            var child = VisualTreeHelper.GetChild(start, i);
            yield return child;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            var count2 = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count2; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                queue.Enqueue(child);
            }
        }
    }

    static public UIElement FindElementByName(UIElement element, string name)
    {
        if (element.XamlRoot != null && element.XamlRoot.Content != null)
        {
            var ele = (element.XamlRoot.Content as FrameworkElement).FindName(name);
            if (ele != null)
            {
                return ele as UIElement;
            }
        }
        return null;
    }

    // Confirmation of Action
    static public void AnnounceActionForAccessibility(UIElement ue, string annoucement, string activityID)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(ue);
        peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted,
                                    AutomationNotificationProcessing.ImportantMostRecent, annoucement, activityID);
    }

    static public void GoBackWithParameter(object paramForPreviousPage, Frame frame)
    {
        var backStack = frame.BackStack;
        if (backStack == null || backStack.Count == 0)
            return;

        var lastIndex = backStack.Count - 1;
        var lastEntry = backStack[lastIndex];

        var newEntry = new PageStackEntry(
            lastEntry.SourcePageType,
            paramForPreviousPage,
            lastEntry.NavigationTransitionInfo);

        backStack[lastIndex] = newEntry;

        if (frame.CanGoBack)
            frame.GoBack();
    }

    public static string GetWindowName(string windowName)
    {
        return $"{windowName} — CDPI UI";
    }

    // https://github.com/microsoft/microsoft-ui-xaml/issues/934#issuecomment-2304875883

    public static class CleanUp
    {
        public static void FrameworkElement(FrameworkElement element)
        {
            var count = VisualTreeHelper.GetChildrenCount(element);
            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(element, index);
                if (child is FrameworkElement childElement)
                {
                    FrameworkElement(childElement);
                }
            }

            switch (element)
            {
                case ItemsControl itemsControl:
                    itemsControl.ItemsSource = null;
                    break;
                case ItemsRepeater itemsRepeater:
                    itemsRepeater.ItemsSource = null;
                    break;
                case TabView tabView:
                    tabView.TabItemsSource = null;
                    break;
            }
        }
    }

    public static Uri GetUriFromString(string input)
    {

        if (Uri.TryCreate(input, UriKind.Absolute, out Uri result) && File.Exists(Path.Combine(Core.Data.Directories.CurrentDirectory, result.OriginalString.Replace("ms-appx:///", ""))))
        {
            return result;
        }
        else
        {
            return new Uri("ms-appx:///Assets/Store/empty.png");
        }
    }

    public static void LoadInstalledComponentsList(ObservableCollection<ViewComponentModel> list)
    {
        list.Clear();
        List<DatabaseStoreItem> items = DatabaseHelper.Instance.GetItemsByType("component");
        foreach (var item in items)
        {

            list.Add(new()
            {
                StoreId = item.Id,
                DisplayName = item.ShortName,
                ImageSource = new BitmapImage(GetUriFromString(LScriptLangHelper.ExecuteScript(item.IconPath))),
                IsUsedForAutorun = SettingsManager.Instance.GetValue<bool>(["CONFIGS", item.Id], "usedForAutorun")
            });
        }
    }

    // https://github.com/FrozenAssassine/Fastedit/blob/v2.9.1/Fastedit/Core/KeyHelper.cs
    public static bool IsKeyPressed(VirtualKey key)
    {
        return Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
    }

    public static ApplicationTheme ConvertTheme(ElementTheme theme)
    {
        switch (theme)
        {
            case ElementTheme.Light:
                return ApplicationTheme.Light;
            case ElementTheme.Dark:
                return ApplicationTheme.Dark;
            case ElementTheme.Default:
                var defaultTheme = new Windows.UI.ViewManagement.UISettings();
                return defaultTheme.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString() == "#FF000000"
                    ? ApplicationTheme.Dark : ApplicationTheme.Light;

            default:
                return ApplicationTheme.Light;
        }
    }

}
