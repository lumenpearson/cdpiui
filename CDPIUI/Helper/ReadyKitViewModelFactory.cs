using CDPIUI.Controls.Store;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper.LScript;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CDPIUI.Helper
{
    internal static class ReadyKitViewModelFactory
    {
        public static StoreViewBundleItem Create(ReadyKitModel kit)
        {
            List<RepoItemModel> kitItems = (kit.items ?? [])
                .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
                .OfType<RepoItemModel>()
                .ToList();

            return new StoreViewBundleItem
            {
                KitId = kit.store_id ?? string.Empty,
                CardTitle = kit.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(
                    kit.name,
                    StoreLocalizationHelper.GetStoreLikeLocale()),
                CardSubtitle = LScriptLangHelper.ExecuteScript(
                    kit.small_description,
                    StoreLocalizationHelper.GetStoreLikeLocale()),
                CardImageSource = CreateIcon(kit.icon),
                CardBackgroundBrush = ReadyKitBrushFactory.Create(
                    kitItems.Select(item => item.background),
                    kit.background),
                Items = kitItems
                    .Select(ViewStoreItemModel.ConvertFromRepoItemModel)
                    .ToList()
            };
        }

        // A kit without an icon threw UriFormatException for new Uri("").
        private static BitmapImage CreateIcon(string icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
            {
                return null;
            }
            return Uri.TryCreate(LScriptLangHelper.ExecuteScript(icon), UriKind.Absolute, out Uri uri)
                ? new BitmapImage(uri)
                : null;
        }
    }
}
