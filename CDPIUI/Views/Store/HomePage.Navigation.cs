using System.Collections.Specialized;
using CDPIUI.Controls.Store;
using CDPIUI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace CDPIUI;

public sealed partial class HomePage
{
    private void StoreItemButton_Click(UIElement sender)
    {
        if (sender is StoreItemLargeButton btn && btn.StoreId is string sid)
        {
            PrepareToConnectedForwardAnimate(btn.imageElement);

            StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage),
                new NameValueCollection() { { "itemId", sid } }, new SuppressNavigationTransitionInfo());
        }
        else if (sender is StoreItemSmallButton smallBtn && smallBtn.StoreId is string smallSid)
        {
            PrepareToConnectedForwardAnimate(smallBtn.imageElement);

            StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage),
                new NameValueCollection() { { "itemId", smallSid } }, new SuppressNavigationTransitionInfo());
        }
    }

    private void CategoryHeaderClick(StoreCategoryButton sender)
    {
        PrepareToConnectedForwardAnimate(sender.textElement, new DirectConnectedAnimationConfiguration());

        StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.CategoryViewPage),
            new NameValueCollection() { { "categoryId", sender.Id } }, new SuppressNavigationTransitionInfo());

    }

    private void ReadyKitButton_Click(StoreReadyKitButton sender)
    {
        PrepareToConnectedForwardAnimate(sender.ImageElement);

        StoreWindow.Instance.NavigateSubPage(
            typeof(Views.Store.ReadyKitViewPage),
            new NameValueCollection() { { "kitId", sender.KitId } },
            new SuppressNavigationTransitionInfo());
    }

    private void BundlesCategoryButton_Click(StoreCategoryButton sender)
    {
        PrepareToConnectedForwardAnimate(sender.textElement, new DirectConnectedAnimationConfiguration());
        StoreWindow.Instance.NavigateSubPage(
            typeof(Views.Store.ReadyKitsViewPage),
            null,
            new SuppressNavigationTransitionInfo());
    }

    private void StoreLoadFixButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenHelp("FixDatabaseLoadIssue");
    }

    private void WhatIsStoreButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenHelp("WelcomeToStore");
    }

    private void AskSupportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = UrlOpenHelper.LaunchReportUrl();
    }

    private static void OpenHelp(string page)
    {
        Commands.CommandsHandler.HandleCommand(
            $"cdpiui://Help/Store/{page.Trim('/')}/");
    }
}
