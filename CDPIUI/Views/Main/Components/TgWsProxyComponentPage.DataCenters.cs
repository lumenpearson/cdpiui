using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Extensions;
using CDPIUI.Helper.TgWsProxy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CDPIUI.Views.Main.Components;

public sealed partial class TgWsProxyComponentPage
{
    private void RemoveElement(string guid)
    {
        _telegramDataCenterModels.Remove(_telegramDataCenterModels.FirstOrDefault(x => x.Guid == guid));
    }

    private async Task EditElement(string guid)
    {
        try
        {
            var el = _telegramDataCenterModels.FirstOrDefault(x => x.Guid == guid);
            var dialog = new TgWsDataCenterEditContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Number = el?.Number,
                Ip = el?.Ip,
                Title = _localizer.GetLocalizedString("EditDataCenter")
            };

            await dialog.ShowQueuedAsync();

            if (dialog.Result)
            {
                el.Number = dialog.Number;
                el.Ip = dialog.Ip;
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(TgWsProxyComponentPage), exception.ToString());
        }
    }

    private async void PasteFromBufferButton_Click(object sender, RoutedEventArgs e)
    {
        DataPackageView dataPackageView = Clipboard.GetContent();

        if (dataPackageView.Contains(StandardDataFormats.Text) is true)
        {
            string text = await dataPackageView.GetTextAsync();

            var dataCenters = TgWsProxyStartupArguments.FindDataCenters(text);
            Debug.WriteLine(dataCenters.Count);
            if (dataCenters.Count > 0)
            {
                foreach (var (number, ip) in dataCenters)
                {
                    var existItem = _telegramDataCenterModels.FirstOrDefault(x => x.Number == number);
                    if (existItem != null)
                    {
                        existItem.Ip = ip;
                    }
                    else
                    {
                        _telegramDataCenterModels.Add(new()
                        {
                            Guid = Guid.NewGuid().ToString(),
                            Number = number,
                            Ip = ip
                        });
                    }
                }

                ShowInfoBar(_localizer.GetLocalizedString("PasteComplete"), InfoBarSeverity.Success);
                return;
            }
            ShowInfoBar(_localizer.GetLocalizedString("PasteFailure"), InfoBarSeverity.Warning);

        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TgWsDataCenterEditContentDialog()
        {
            XamlRoot = this.XamlRoot,
            Title = _localizer.GetLocalizedString("AddDataCenter")
        };

        await dialog.ShowQueuedAsync();
        if (dialog.Result)
        {
            _telegramDataCenterModels.Add(new()
            {
                Guid = Guid.NewGuid().ToString(),
                Number = dialog.Number,
                Ip = dialog.Ip,
            });
        }
    }

    private void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        string targetText = string.Empty;
        foreach (var model in _telegramDataCenterModels)
        {
            targetText += $"{model.Number}:{model.Ip}\n";
        }

        DataPackage dataPackage = new();
        dataPackage.RequestedOperation = DataPackageOperation.Copy;
        dataPackage.SetText(targetText);
        Clipboard.SetContent(dataPackage);

        ShowInfoBar(_localizer.GetLocalizedString("CopyComplete"), InfoBarSeverity.Success);
        return;
    }
}
