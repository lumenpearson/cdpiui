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
    public ObservableCollection<BlockCheck2SiteListItem> AvailableSiteLists { get; } = [];
    public ObservableCollection<BlockCheck2SiteListItem> SelectedSiteLists { get; } = [];

    public bool IsSiteListLoading => _siteLists.IsSiteListLoading;
    public string? SiteListLoadError => _siteLists.SiteListLoadError;
    public int SelectedSiteListCount => _siteLists.SelectedSiteListCount;
    public int SelectedDomainCount => _siteLists.SelectedDomainCount;

    public Task LoadSiteListsAsync() => _siteLists.LoadAsync();
    public BlockCheck2SiteListItem AddCustomSiteList(string filePath, string sourceName) => _siteLists.AddCustom(filePath, sourceName);
    public Task<BlockCheck2SiteListItem> UpdateCustomSiteListAsync(string content, string displayName, string sourceName, CancellationToken cancellationToken = default) =>
        _siteLists.UpdateCustomAsync(content, displayName, sourceName, cancellationToken);
    public void SelectSiteList(BlockCheck2SiteListItem item) => _siteLists.Select(item);
    public void RemoveSiteList(BlockCheck2SiteListItem item) => _siteLists.Remove(item);
    public void SetSelectedSiteLists(IEnumerable<BlockCheck2SiteListItem> selected) => _siteLists.SetSelected(selected);

    public async Task<BlockCheck2PreparationPreview> BuildPreparationPreviewAsync(
        CancellationToken cancellationToken = default)
    {
        BlockCheckSessionRequest request = CreateSessionRequest();
        double requestTimeout = _options.RequestTimeoutSeconds;
        double startupGrace = _options.StartupGraceSeconds;
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
        if (_results.LastReport == null)
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
