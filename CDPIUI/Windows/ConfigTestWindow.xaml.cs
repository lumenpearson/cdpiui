using System.Collections.Generic;
using System.Linq;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Default;
using CDPIUI.Helper.AddOns;
using CDPIUI.ViewModels.PresetTest;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ConfigTestWindow : TemplateWindow, IConfigTestWindowView
{
    public ConfigTestWindowViewModel ViewModel { get; }

    public string ComponentIdToTest
    {
        get => ViewModel.ComponentIdToTest;
        set => ViewModel.ComponentIdToTest = value;
    }

    public ConfigTestWindow()
    {
        ViewModel = new ConfigTestWindowViewModel(this);
        InitializeComponent();

        WindowTitle = Localizer.Get().GetLocalizedString("PresetTestWindowTitle");

        IconUri = @"Assets/Icons/GoodCheck.ico";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        WindowMinSize = new System.Windows.Size(1100, 520);

        ViewModel.LoadComponents();

        Closed += (_, _) => ViewModel.OnWindowClosed();
    }

    public IReadOnlyList<ConfigItem> GetSelectedPresets() => PresetListView.SelectedItems.OfType<ConfigItem>().ToList();

    public void ClearLog()
    {
        OutputParagraph.Inlines.Clear();
    }

    public void AppendLogLine(IReadOnlyList<TestLogSegment> segments)
    {
        foreach (TestLogSegment segment in segments)
        {
            OutputParagraph.Inlines.Add(new Run
            {
                Text = segment.Text,
                Foreground = BrushFor(segment.Color)
            });
        }
        OutputParagraph.Inlines.Add(new LineBreak());
        OutputScrollViewer.ChangeView(null, OutputScrollViewer.ScrollableHeight, null);
    }

    private async void ApplyPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PresetResultViewModel vm)
            await ViewModel.ApplyPresetAsync(vm, restartIfRunning: false);
    }

    private async void RunPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PresetResultViewModel vm)
            await ViewModel.ApplyPresetAsync(vm, restartIfRunning: true);
    }

    private static SolidColorBrush BrushFor(TestLogColor color)
    {
        Color c = color switch
        {
            TestLogColor.Green => Color.FromArgb(255, 87, 166, 74),
            TestLogColor.Cyan => Color.FromArgb(255, 78, 201, 176),
            TestLogColor.Yellow => Color.FromArgb(255, 229, 192, 123),
            TestLogColor.Red => Color.FromArgb(255, 244, 71, 71),
            TestLogColor.Gray => Color.FromArgb(255, 150, 150, 150),
            TestLogColor.White => Color.FromArgb(255, 230, 230, 230),
            _ => Color.FromArgb(255, 220, 220, 220),
        };
        return new SolidColorBrush(c);
    }
}
