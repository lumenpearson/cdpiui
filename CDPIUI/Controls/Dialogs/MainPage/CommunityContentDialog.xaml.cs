using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CDPIUI.Helper;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Dialogs.MainPage;

public sealed partial class CommunityContentDialog : ContentDialog
{
    private ObservableCollection<UrlViewModel> _models = [];

    private ILocalizer _localizer = Localizer.Get();

    public CommunityContentDialog()
    {
        InitializeComponent();

        MainListView.ItemsSource = _models;

        Init();
    }

    private UrlViewModel CreateNewModel(string name, string description, string url)
    {
        string locName = _localizer.GetLocalizedString(name);
        string locDesc = _localizer.GetLocalizedString(description);
        return new()
        {
            Name = locName,
            Description = locDesc,
            Url = url
        };
    }

    private void Init()
    {
        _models.Clear();
        _models.Add(CreateNewModel("TelegramMainUrlName", "TelegramMainUrlDescription", UrlOpenHelper.TelegramMainUrl));
        _models.Add(CreateNewModel("NTCPartyName", "NTCPartyDescription", UrlOpenHelper.NTCParty));
        _models.Add(CreateNewModel("BBDName", "BBDDescription", UrlOpenHelper.BBD));

        _models.Add(CreateNewModel("TelegramMemeUrlName", "TelegramMemeUrlDescription", UrlOpenHelper.TelegramMemeUrl));
        _models.Add(CreateNewModel("TelegramLUrlName", "TelegramLUrlDescription", UrlOpenHelper.TelegramLUrl));
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var data = (sender as FrameworkElement).DataContext as UrlViewModel;

        _ = UrlOpenHelper.LaunchUrl(data.Url);
    }
}
