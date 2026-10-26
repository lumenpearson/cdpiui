using System;
using System.Collections.ObjectModel;
using CDPIUI.Controls.Navigation;
using CDPIUI.Default;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class AppFeaturesWindow : TemplateWindow
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private bool _isReloading;

    public ObservableCollection<AppFeatureViewModel> Features { get; } = [];

    public AppFeaturesWindow()
    {
        InitializeComponent();

        WindowTitle = _localizer.GetLocalizedString("AppFeaturesWindowTitle");
        IconUri = @"Assets/favicon.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        DisableResizeFeature();

        ReloadFeatures();

        _localizer.LanguageChanged += Localizer_LanguageChanged;
        Closed += AppFeaturesWindow_Closed;
    }

    private void ReloadFeatures()
    {
        int selectedIndex = Math.Max(FeatureListView.SelectedIndex, 0);
        var localizedFeatures = AppFeaturesCatalog.CreateLocalized(_localizer);
        DataTemplate contentTemplate = (DataTemplate)RootGrid.Resources["FeatureContentTemplate"];

        _isReloading = true;
        Features.Clear();
        FeatureContentViewer.Items.Clear();

        foreach (var feature in localizedFeatures)
        {
            Features.Add(feature);
            FeatureContentViewer.Items.Add(new AnimatedHorizontalContentItem
            {
                Content = new ContentControl
                {
                    Content = feature,
                    ContentTemplate = contentTemplate,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                }
            });
        }

        selectedIndex = Math.Min(selectedIndex, Features.Count - 1);
        FeatureListView.SelectedIndex = selectedIndex;
        _isReloading = false;

        if (FeatureContentViewer.IsLoaded && selectedIndex >= 0)
            FeatureContentViewer.GoTo(selectedIndex);
    }

    private void FeatureListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int selectedIndex = FeatureListView.SelectedIndex;
        if (_isReloading || selectedIndex < 0 || selectedIndex >= FeatureContentViewer.Items.Count)
            return;

        FeatureContentViewer.GoTo(selectedIndex);
    }

    private void Localizer_LanguageChanged(object sender, LanguageChangedEventArgs e)
    {
        WindowTitle = _localizer.GetLocalizedString("AppFeaturesWindowTitle");
        ReloadFeatures();
    }

    private void AppFeaturesWindow_Closed(object sender, WindowEventArgs args)
    {
        _localizer.LanguageChanged -= Localizer_LanguageChanged;
        Closed -= AppFeaturesWindow_Closed;
    }
}
