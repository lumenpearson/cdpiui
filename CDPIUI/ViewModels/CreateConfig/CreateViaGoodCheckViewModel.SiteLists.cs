using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Views.CreateConfigUtil;

namespace CDPIUI.ViewModels.CreateConfig;

// The site lists step and the strategy lists step.
public sealed partial class CreateViaGoodCheckViewModel
{
    private readonly List<OneSiteListOneStrategyModeElement> _oneSiteListOneStrategyModeElements = [];

    private List<GoodCheckStrategiesList> _strategiesLists = [];
    private GoodCheckStrategiesList _selectedStrategiesList;
    private bool _isSeparateMode;
    private bool _isSelectedSiteListTipVisible = true;

    /// <summary>The site lists the user can add from the flyout.</summary>
    public ObservableCollection<SiteListElement> SiteListTemplates { get; } = [];

    /// <summary>The site lists GoodCheck checks.</summary>
    public ObservableCollection<SiteListElement> SelectedSiteLists { get; } = [];

    public List<GoodCheckStrategiesList> StrategiesLists
    {
        get => _strategiesLists;
        private set => SetProperty(ref _strategiesLists, value);
    }

    /// <summary>The strategy list for all site lists.</summary>
    public GoodCheckStrategiesList SelectedStrategiesList
    {
        get => _selectedStrategiesList;
        set
        {
            if (SetProperty(ref _selectedStrategiesList, value))
                CanGoForward = value != null;
        }
    }

    /// <summary>Each site list has its own strategy list.</summary>
    public bool IsSeparateMode
    {
        get => _isSeparateMode;
        set
        {
            if (!SetProperty(ref _isSeparateMode, value))
                return;

            OnPropertyChanged(nameof(IsAllListsOneStrategy));
            CanGoForward = SelectedStrategiesList != null || value;
        }
    }

    public bool IsAllListsOneStrategy => !IsSeparateMode;

    public bool IsSelectedSiteListTipVisible
    {
        get => _isSelectedSiteListTipVisible;
        private set => SetProperty(ref _isSelectedSiteListTipVisible, value);
    }

    /// <summary>Updates the next button and the empty tip of the site lists step.</summary>
    public void RefreshSiteListsState()
    {
        if (_currentState != GoodCheckWizardState.SiteListsSettings)
            return;

        bool hasSiteLists = SelectedSiteLists.Count > 0;
        CanGoForward = hasSiteLists;
        IsSelectedSiteListTipVisible = !hasSiteLists;
    }

    public void LoadSiteList(IEnumerable<SiteListElement> items)
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => LoadSiteList(items));
            return;
        }

        SiteListTemplates.Clear();

        foreach (var item in items)
        {
            SiteListTemplates.Add(item);
        }
    }

    /// <summary>
    /// Copies the site list file to the local site lists. Returns <see langword="null"/>
    /// when the file is not a site list.
    /// </summary>
    public async Task<SiteListElement> ImportSiteListFileAsync(string filePath)
    {
        bool isCorrect = await DomainValidationHelper.IsFileCorrectSiteList(filePath, DomainValidationHelper.CheckMode.Quick);

        if (!isCorrect)
        {
            return null;
        }

        string targetFolder = Path.Combine(Directories.StoreItemsDirectory, SharedConstants.LocalUserItemsId, SharedConstants.LocalUserItemSiteListsFolder);

        string newFilePath = FileSystemService.CopyTxtWithUniqueName(filePath, targetFolder);

        return new()
        {
            Directory = newFilePath,
            Name = Path.GetFileName(newFilePath),
            PackId = SharedConstants.LocalUserItemsId,
            PackName = _localizer.GetLocalizedString("LocalData"),
        };
    }

    public bool AddSiteListElement(SiteListElement site)
    {
        if (site == null)
            return false;

        bool exists = SelectedSiteLists.Any(c => c.Directory == site.Directory);

        if (exists)
            return false;

        _uiDispatcher.TryEnqueue(() => SelectedSiteLists.Add(site));

        return true;
    }

    public Task RemoveSiteListElementAsync(SiteListElement tagElement)
    {
        if (tagElement == null)
            return Task.CompletedTask;

        _uiDispatcher.TryEnqueue(() =>
        {
            var target = SelectedSiteLists.FirstOrDefault(se => ReferenceEquals(se, tagElement));

            if (target == null)
            {
                var normalized = FileSystemService.NormalizeDirectory(tagElement.Directory);
                target = SelectedSiteLists.FirstOrDefault(se =>
                    string.Equals(FileSystemService.NormalizeDirectory(se.Directory), normalized, StringComparison.OrdinalIgnoreCase));
            }

            if (target == null)
                return;

            try
            {
                SelectedSiteLists.Remove(target);
            }
            catch (Exception exception)
            {
                Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
            }
        });

        return Task.CompletedTask;
    }

    public void ClearAllSiteListElements()
    {
        _uiDispatcher.TryEnqueue(() => SelectedSiteLists.Clear());
    }

    /// <summary>Sets the strategy list the user picked for one site list.</summary>
    public void SetStrategyForSiteList(string filePath, string strategyFilePath)
    {
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
    }

    private async Task InitGoodCheckSiteListsPage()
    {
        SetLoadingMode(true, isIndeterminate: true, cancellationAvailable: false, headerText: _localizer.GetLocalizedString("WorkingOnIt"));

        List<SiteListElement> siteLists = await SiteListHelper.GetAllAvailableSiteListTemplatesAsync();

        LoadSiteList(siteLists);

        SwitchState(GoodCheckWizardState.SiteListsSettings);
        CanGoForward = false;

        StrategiesLists = GoodCheckResultViewHelper.GetAvailableStrategiesLists(ComponentId); // TODO: rase exception if elements count zero

        SetLoadingMode(false);
    }

    private void InitGoodCheckStrategiesListsPage()
    {
        _oneSiteListOneStrategyModeElements.Clear();

        List<SiteListElement> siteLists = [.. SelectedSiteLists];
        _view.ClearStrategyChoosers();

        if (_strategiesLists.Count == 0)
        {
            // The add-on is installed: it has no strategy lists for this component.
            _ = ShowErrorDialog(_localizer.GetLocalizedString("GoodCheckNoStrategyLists")); // ShowErrorDialog catches and logs its own errors.
            return;
        }

        foreach (SiteListElement siteList in siteLists)
        {
            _view.AddStrategyChooser(siteList, _strategiesLists);

            _oneSiteListOneStrategyModeElements.Add(new OneSiteListOneStrategyModeElement
            {
                SiteListFilePath = siteList.Directory,
                StrategiesListFilePath = _strategiesLists[0].FilePath,
            });
        }
    }
}
