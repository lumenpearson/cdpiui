using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Interop;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CDPIUI.Controls.Dialogs;
using CDPIUI.Core;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUI3Localizer;

namespace CDPIUI.Helper.UserExperience;

public partial class ConsoleFontHelper : INotifyPropertyChanged
{
    public static ConsoleFontHelper Instance { get; } = new();

    #region Settings

    private FontFamily _fontFamily;
    public FontFamily FontFamily
    {
        get => _fontFamily;
        set
        {
            if (ReferenceEquals(_fontFamily, value))
                return;

            _fontFamily = value;

            SettingsManager.Instance.SetValue<string>("PSEUDOCONSOLE", "fontFamily", _fontFamily.Source.ToString());
            Notify(nameof(FontFamily));
        }
    }

    private double _fontSize;
    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (Equals(_fontSize, value))
                return;

            _fontSize = value;

            SettingsManager.Instance.SetValue<double>("PSEUDOCONSOLE", "fontSize", _fontSize);
            Notify(nameof(FontSize));
        }
    }

    #endregion

    #region UI

    private bool _onlyMonospace = SettingsManager.Instance.GetValueOrDefault<bool>("PSEUDOCONSOLE", "showOnlyMonospaceFontsInView", defaultValue: true);
    public bool OnlyMonospace
    {
        get => _onlyMonospace;
        set
        {
            if (Equals(_onlyMonospace, value))
                return;

            _onlyMonospace = value;
            SettingsManager.Instance.SetValue<bool>("PSEUDOCONSOLE", "showOnlyMonospaceFontsInView", _onlyMonospace);
            LoadFonts();
            Notify(nameof(OnlyMonospace));
        }
    }

    private readonly ObservableCollection<FontFamily> _availableFonts = [];
    public ObservableCollection<FontFamily> AvailableFonts
    {
        get
        {
            if (_availableFonts is null || _availableFonts.Count == 0)
            {
                LoadFonts();
                return _availableFonts;
            }
            return _availableFonts;
        }
    }

    public FontFamily SelectedFontFamilyInCollection
    {
        get => AvailableFonts.FirstOrDefault(x => x.Source == FontFamily.Source);
    }

    public List<double> FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36, 48, 72];

    #endregion

    public event PropertyChangedEventHandler PropertyChanged;

    public async Task ShowFontSettingsDialogForXamlRoot(XamlRoot xamlRoot)
    {
        try
        {
            FontSettingsContentDialog dialog = new() { XamlRoot = xamlRoot };

            if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
            {
                FontFamily = dialog.FontName;
                FontSize = dialog.FontSize;
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConsoleFontHelper), exception.ToString());
        }
    }

    private ConsoleFontHelper()
    {
        FontFamily = GetFont(SettingsManager.Instance.GetValue<string>("PSEUDOCONSOLE", "fontFamily"));
        FontSize = SettingsManager.Instance.GetValue<double>("PSEUDOCONSOLE", "fontSize");
    }



    private static FontFamily GetFont(string fontFamily)
    {
        try
        {
            return new FontFamily(fontFamily);
        }
        catch
        {
            return new FontFamily("Consolas");
        }
    }

    private void LoadFonts()
    {
        _availableFonts.Clear();

        foreach (System.Drawing.FontFamily font in System.Drawing.FontFamily.Families)
        {
            if (!OnlyMonospace)
            {
                _availableFonts.Add(new FontFamily(font.Name));
                continue;
            }

            if (font.IsStyleAvailable(System.Drawing.FontStyle.Regular))
            {
                float diff;
                using (System.Drawing.Font _font = new System.Drawing.Font(font, 16))
                {
                    diff = TextRenderer.MeasureText("WWW", _font).Width - TextRenderer.MeasureText("...", _font).Width;
                }
                if (Math.Abs(diff) < float.Epsilon * 2)
                {
                    _availableFonts.Add(new FontFamily(font.Name));
                }
            }

        }

        Notify(nameof(AvailableFonts));
        Notify(nameof(SelectedFontFamilyInCollection));
    }

    private void Notify(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
