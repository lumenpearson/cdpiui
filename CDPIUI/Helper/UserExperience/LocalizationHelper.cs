using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Helper.Items;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Globalization;
using WinUI3Localizer;

namespace CDPIUI.Helper.UserExperience;

internal sealed class LocalizationHelper : INotifyPropertyChanged
{
    private static LocalizationHelper _instance;
    private static readonly object SyncRoot = new();
    public static LocalizationHelper Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new LocalizationHelper();
                return _instance;
            }
        }
    }

    private readonly ILocalizer _localizer;
    private LanguageSelectModel _currentLanguage;

    public ObservableCollection<LanguageSelectModel> Languages { get; } = [];

    public LanguageSelectModel CurrentLanguage
    {
        get => _currentLanguage;
        private set
        {
            if (ReferenceEquals(_currentLanguage, value))
                return;

            _currentLanguage = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private LocalizationHelper()
    {
        _localizer = Localizer.Get();

        Languages.Add(new()
        {
            Id = "en-us",
            DisplayName = _localizer.GetLocalizedString("en-us")
        });

        Languages.Add(new()
        {
            Id = "ru",
            DisplayName = _localizer.GetLocalizedString("ru")
        });

        CurrentLanguage = FindLanguage(_localizer.GetCurrentLanguage());
        _localizer.LanguageChanged += Localizer_LanguageChanged;
    }

    // Falls back to the first language: an unknown id left CurrentLanguage null and the
    // next access to CurrentLanguage.Id threw.
    private LanguageSelectModel FindLanguage(string id)
    {
        return Languages.FirstOrDefault(
            language => string.Equals(
                language.Id,
                id,
                StringComparison.OrdinalIgnoreCase))
            ?? Languages.First();
    }

    private void Localizer_LanguageChanged(
        object sender,
        LanguageChangedEventArgs e)
    {
        CurrentLanguage = FindLanguage(e.CurrentLanguage);

        ApplicationInfo.Instance.SetLocalization(CurrentLanguage.Id);
    }

    public async Task SaveLanguageSettings(string id)
    {
        if (string.Equals(
            id,
            _localizer.GetCurrentLanguage(),
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _localizer.SetLanguage(id);
        SettingsManager.Instance.SetValue<string>("SYSTEM", "language", id);
    }

    public async void HandleCollectionLanguageChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is LanguageSelectModel language)
            await SaveLanguageSettings(language.Id);
    }
}
