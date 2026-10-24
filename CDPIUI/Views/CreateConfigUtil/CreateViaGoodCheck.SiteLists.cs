using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.Basic.Settings;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Extensions;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Unidecode.NET;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI.Views.CreateConfigUtil;

public sealed partial class CreateViaGoodCheck
{
    public void LoadSiteList(IEnumerable<SiteListElement> items)
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => LoadSiteList(items));
            return;
        }

        SiteListTemplatesStackPanel.Children.Clear();

        foreach (var item in items)
        {
            var btn = new SitelistButton
            {
                Title = item.Name,
                PackName = item.PackName,
                PackId = item.PackId,
                Directory = item.Directory,
                Tag = item,
            };

            btn.Click += () =>
            {
                SelectSiteListFlyout.Hide();
                AddSiteListElement(item);
            };

            SiteListTemplatesStackPanel.Children.Add(btn);
        }
    }

    private async Task InitGoodCheckSiteListsPage()
    {

        SetLoadingMode(true, isIndeterminate: true, cancellationAvailable: false, headerText: _localizer.GetLocalizedString("WorkingOnIt"));

        List<SiteListElement> siteLists = await SiteListHelper.GetAllAvailableSiteListTemplatesAsync();


        LoadSiteList(siteLists);

        SwitchState(States.SiteListsSettings);
        GoForwardButton.IsEnabled = false;

        _strategiesLists = GoodCheckResultViewHelper.GetAvailableStrategiesLists(ComponentId);

        StrategiesListCombobox.ItemsSource = _strategiesLists; // TODO: rase exception if elements count zero

        SetLoadingMode(false);

        await Task.CompletedTask;
    }

    public void InitGoodCheckStrategiesListsPage()
    {
        _sitelistButtons.Clear();
        _oneSiteListOneStrategyModeElements.Clear();

        foreach (GoodCheckSitelistButton button in SelectedSitelistStackPanel.Children.OfType<GoodCheckSitelistButton>())
        {
            _sitelistButtons.Add(button);
        }
        OneListOneStrategyStackPanel.Children.Clear();

        if (_strategiesLists.Count == 0)
        {
            // The add-on is installed: it has no strategy lists for this component.
            _ = ShowErrorDialog(_localizer.GetLocalizedString("GoodCheckNoStrategyLists")); // ShowErrorDialog catches and logs its own errors.
            return;
        }

        foreach (GoodCheckSitelistButton button in _sitelistButtons)
        {
            GoodCheckSitelistStrategyChooserControl checkListChooseControl = new()
            {
                Title = button.Title,
                PackName = button.PackName,
                FilePath = button.FilePath,
                StrategiesLists = _strategiesLists
            };
            checkListChooseControl.StrategyChanged += (tuple) =>
            {
                string filePath = tuple.Item1;
                string strategyFilePath = tuple.Item2;

                var existing = _oneSiteListOneStrategyModeElements
                    .FirstOrDefault(x => x.SiteListFilePath == filePath);

                if (existing == null)
                {
                    _oneSiteListOneStrategyModeElements.Add(new OneSiteListOneStrategyModeElement
                    {
                        SiteListFilePath = filePath,
                        StrategiesListFilePath = strategyFilePath
                    });
                }
                else
                {
                    existing.StrategiesListFilePath = strategyFilePath;
                }
            };

            _oneSiteListOneStrategyModeElements.Add(new OneSiteListOneStrategyModeElement
            {
                SiteListFilePath = button.FilePath,
                StrategiesListFilePath = _strategiesLists[0].FilePath,
            });

            OneListOneStrategyStackPanel.Children.Add(checkListChooseControl);
        }
    }

    private async void ChooseFileButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = string.Empty;

        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Title = _localizer.GetLocalizedString("ChooseSiteList");
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Filter = $"{_localizer.GetLocalizedString("TextFiles")} (*.txt)|*.txt";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
            }
            else
            {
                return;
            }
        }

        bool isCorrect = await DomainValidationHelper.IsFileCorrectSiteList(filePath, DomainValidationHelper.CheckMode.Quick);

        if (!isCorrect)
        {
            FileIncorrectWarning.IsOpen = true;
            FileIncorrectWarning.Visibility = Visibility.Visible;
            OpenStoryboard.Begin();
            return;
        }

        string targetFolder = Path.Combine(Directories.StoreItemsDirectory, SharedConstants.LocalUserItemsId, SharedConstants.LocalUserItemSiteListsFolder);

        string newFilePath = FileSystemService.CopyTxtWithUniqueName(filePath, targetFolder);

        SiteListElement siteListElement = new()
        {
            Directory = newFilePath,
            Name = Path.GetFileName(newFilePath),
            PackId = SharedConstants.LocalUserItemsId,
            PackName = _localizer.GetLocalizedString("LocalData"),
        };

        AddSiteListElement(siteListElement);

    }

    public bool AddSiteListElement(SiteListElement site)
    {
        CloseStoryboard.Begin();
        if (site == null)
            return false;

        bool exists = false;

        exists = SelectedSitelistStackPanel.Children
                    .OfType<GoodCheckSitelistButton>()
                    .Any(c =>
                    {
                        return (c.FilePath == site.Directory);
                    });

        if (exists)
            return false;

        var element = site;
        var btn = new GoodCheckSitelistButton
        {
            Title = element.Name,
            PackName = element.PackName,
            FilePath = element.Directory,
            Tag = element,
        };

        btn.RemoveElement = () =>
        {
            _ = RemoveSiteListElementByTagAsync(element);
        };

        DispatcherQueue.TryEnqueue(() =>
        {
            SelectedSitelistStackPanel.Children.Add(btn);
        });

        return true;
    }

    public Task RemoveSiteListElementByTagAsync(SiteListElement tagElement)
    {
        if (tagElement == null)
            return Task.CompletedTask;

        DispatcherQueue.TryEnqueue((DispatcherQueueHandler)(() =>
        {
            var target = SelectedSitelistStackPanel.Children
                         .OfType<GoodCheckSitelistButton>()
                         .FirstOrDefault((Func<GoodCheckSitelistButton, bool>)(c => c.Tag is SiteListElement se && ReferenceEquals((object)se, tagElement)));

            if (target == null)
            {
                var normalized = FileSystemService.NormalizeDirectory(tagElement.Directory);
                target = SelectedSitelistStackPanel.Children
                         .OfType<GoodCheckSitelistButton>()
                         .FirstOrDefault((Func<GoodCheckSitelistButton, bool>)(c => c.Tag is SiteListElement se &&
                                              string.Equals(FileSystemService.NormalizeDirectory((string)se.Directory), normalized, StringComparison.OrdinalIgnoreCase)));
            }

            if (target == null)
                return;

            try
            {
                target.RemoveElement = null;
                SelectedSitelistStackPanel.Children.Remove(target);
            }
            catch (Exception exception)
            {
                Logger.Instance.CreateWarningLog(nameof(CreateViaGoodCheck), exception.ToString());
            }
        }));

        return Task.CompletedTask;
    }

    public void ClearAllSiteListElements()
    {
        DispatcherQueue.TryEnqueue(() => SelectedSitelistStackPanel.Children.Clear());
    }
}
