using System;
using CDPIUI.Helper.LScript;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace CDPIUI.Helper;

public static partial class UIHelper
{
    public static StoreItemLargeButton CreateLargeButton(string storeId, string imageSource, string price, string title, string backgroundColor, Action<StoreItemLargeButton> action, string developer = "")
    {
        StoreItemLargeButton storeItemLargeButton;

        string eImageSource = LScriptLangHelper.ExecuteScript(imageSource);
        BitmapImage image = new BitmapImage(new Uri(eImageSource));

        Color color = HexToColorConverter(backgroundColor);

        storeItemLargeButton = new StoreItemLargeButton
        {
            StoreId = storeId,
            CardTitle = title,
            CardDeveloper = developer,
            CardImageSource = image,
            CardPrice = price,
            CardBackgroundColor = color,
        };

        storeItemLargeButton.Click += action;

        return storeItemLargeButton;
    }

    public static StoreItemSmallButton CreateSmallButton(string storeId, string imageSource, string price, string title, string developer, string backgroundColor, Action<StoreItemSmallButton> action)
    {
        StoreItemSmallButton storeItemSmallButton;

        string eImageSource = LScriptLangHelper.ExecuteScript(imageSource);
        BitmapImage image = new BitmapImage(new Uri(eImageSource));

        SolidColorBrush solidColorBrush = HexToSolidColorBrushConverter(backgroundColor);

        storeItemSmallButton = new StoreItemSmallButton
        {
            StoreId = storeId,
            CardTitle = title,
            CardImageSource = image,
            CardPrice = price,
            CardDeveloper = developer,
            CardBackgroundBrush = solidColorBrush,
        };

        storeItemSmallButton.Click += action;

        return storeItemSmallButton;
    }
}
