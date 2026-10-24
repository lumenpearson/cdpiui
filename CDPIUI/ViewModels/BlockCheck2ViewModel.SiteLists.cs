using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.GoodCheck;

namespace CDPIUI.ViewModels;

public sealed partial class BlockCheck2ViewModel
{
    private string _generatedSiteListRoot = BlockCheck2SiteListStorage.CreateGeneratedSiteListRoot();
    private bool _siteListsLoaded;
    private bool _isSiteListLoading;
    private string? _siteListLoadError;

    public ObservableCollection<BlockCheck2SiteListItem> AvailableSiteLists { get; } = [];
    public ObservableCollection<BlockCheck2SiteListItem> SelectedSiteLists { get; } = [];

    public bool IsSiteListLoading => _isSiteListLoading;
    public string? SiteListLoadError => _siteListLoadError;
    public int SelectedSiteListCount => SelectedSiteLists.Count;
    public int SelectedDomainCount => SelectedSiteLists.Sum(item => item.DomainCount);

    public async Task LoadSiteListsAsync()
    {
        if (_siteListsLoaded || _isSiteListLoading)
        {
            return;
        }

        _isSiteListLoading = true;
        _siteListLoadError = null;
        Notify(nameof(IsSiteListLoading));
        Notify(nameof(SiteListLoadError));
        try
        {
            List<SiteListElement> items = await SiteListHelper.GetAllAvailableSiteListTemplatesAsync();
            AvailableSiteLists.Clear();
            foreach (SiteListElement item in items
                         .Where(item => !string.IsNullOrWhiteSpace(item.Directory))
                         .Where(item => !BlockCheck2SiteListStorage.IsLegacyGeneratedSiteList(item.Directory!))
                         .OrderBy(item => item.PackName, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var siteListItem = new BlockCheck2SiteListItem(
                    item.Name ?? Path.GetFileName(item.Directory!) ?? "site-list.txt",
                    item.PackName ?? string.Empty,
                    item.Directory!,
                    individualSiteListRoot: _generatedSiteListRoot);
                if (siteListItem.DomainCount > 0 && !siteListItem.DisplayName.Contains("exclude"))
                    AvailableSiteLists.Add(siteListItem);

            }
            _siteListsLoaded = true;
        }
        catch (Exception exception)
        {
            _siteListLoadError = exception.Message;
            Notify(nameof(SiteListLoadError));
        }
        finally
        {
            _isSiteListLoading = false;
            Notify(nameof(IsSiteListLoading));
        }
    }

    public BlockCheck2SiteListItem AddCustomSiteList(string filePath, string sourceName)
    {
        string fullPath = Path.GetFullPath(filePath);
        BlockCheck2SiteListItem? existing = AvailableSiteLists
            .Concat(SelectedSiteLists)
            .FirstOrDefault(
            item => string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing;
        }

        BlockCheck2SiteListItem added = new(
            Path.GetFileName(fullPath),
            sourceName,
            fullPath,
            individualSiteListRoot: _generatedSiteListRoot);
        return added;
    }

    public async Task<BlockCheck2SiteListItem> UpdateCustomSiteListAsync(
        string content,
        string displayName,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        string normalizedContent = BlockCheck2SiteListStorage.NormalizeCustomContent(content);
        if (normalizedContent.Length == 0)
        {
            throw new InvalidDataException(
                WinUI3Localizer.Localizer.Get().GetLocalizedString("BlockCheck2CustomSiteListEmpty"));
        }

        BlockCheck2SiteListItem? item = SelectedSiteLists.FirstOrDefault(candidate => candidate.IsCustom);
        string filePath = item?.FilePath ?? BlockCheck2SiteListStorage.CreateCustomSiteListPath();
        await BlockCheck2SiteListStorage.WriteCustomSiteListAsync(filePath, normalizedContent, cancellationToken);

        if (item == null)
        {
            item = new BlockCheck2SiteListItem(
                displayName,
                sourceName,
                filePath,
                isCustom: true,
                editableContent: normalizedContent,
                individualSiteListRoot: _generatedSiteListRoot);
            SelectedSiteLists.Insert(0, item);
        }
        else
        {
            item.UpdateCustomContent(normalizedContent);
        }

        NotifySiteListSelectionChanged();
        return item;
    }

    public void SelectSiteList(BlockCheck2SiteListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (SelectedSiteLists.Any(selected => string.Equals(
                selected.FilePath,
                item.FilePath,
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedSiteLists.Add(item);
        NotifySiteListSelectionChanged();
    }

    public void RemoveSiteList(BlockCheck2SiteListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (SelectedSiteLists.Remove(item))
        {
            NotifySiteListSelectionChanged();
        }
    }

    public void SetSelectedSiteLists(IEnumerable<BlockCheck2SiteListItem> selected)
    {
        SelectedSiteLists.Clear();
        foreach (BlockCheck2SiteListItem item in selected.DistinctBy(
                     item => item.FilePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            SelectedSiteLists.Add(item);
        }
        NotifySiteListSelectionChanged();
    }

    public async Task<BlockCheck2PreparationPreview> BuildPreparationPreviewAsync(
        CancellationToken cancellationToken = default)
    {
        BlockCheckSessionRequest request = CreateSessionRequest();
        double requestTimeout = _requestTimeoutSeconds;
        double startupGrace = _startupGraceSeconds;
        BlockCheck2PreparationPreview preview = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            BlockCheckSessionPreparationResult prepared = _preparationService.Prepare(request);
            BlockCheckMaximumDurationEstimate? estimate = prepared.Success
                ? _maximumDurationEstimator.Estimate(
                    prepared.Catalog,
                    prepared.Targets,
                    prepared.RunOptions,
                    TimeSpan.FromSeconds(requestTimeout),
                    TimeSpan.FromSeconds(startupGrace))
                : null;
            return new BlockCheck2PreparationPreview(
                prepared.Targets.Count,
                estimate,
                prepared.Issues);
        }, cancellationToken);

        if (!preview.Success)
        {
            CleanupGeneratedSiteListsIfUnviewable();
        }

        return preview;
    }

    public void CleanupGeneratedSiteListsIfUnviewable()
    {
        if (_lastReport == null)
        {
            CleanupGeneratedSiteLists();
        }
    }

    private void CleanupGeneratedSiteLists() =>
        BlockCheck2SiteListStorage.CleanupGeneratedSiteLists(_generatedSiteListRoot);

    private void NotifySiteListSelectionChanged()
    {
        Notify(nameof(SelectedSiteListCount));
        Notify(nameof(SelectedDomainCount));
        Notify(nameof(HasTargetSources));
        Notify(nameof(CanStart));
    }
}
