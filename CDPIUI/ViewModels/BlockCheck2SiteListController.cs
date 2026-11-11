#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.GoodCheck;

namespace CDPIUI.ViewModels;

// Owns list loading and selection; actual UI rows and storage stay behind the backend.
internal sealed class BlockCheck2SiteListController<TItem> : ObservableObject where TItem : class
{
    private readonly ObservableCollection<TItem> _available;
    private readonly ObservableCollection<TItem> _selected;
    private readonly IBlockCheck2SiteListBackend<TItem> _backend;
    private readonly Func<string> _generatedRoot;
    private readonly Action _selectionChanged;
    private bool _siteListsLoaded;
    private bool _isSiteListLoading;
    private string? _siteListLoadError;

    public BlockCheck2SiteListController(ObservableCollection<TItem> available, ObservableCollection<TItem> selected,
        IBlockCheck2SiteListBackend<TItem> backend, Func<string> generatedRoot, Action selectionChanged)
    {
        _available = available; _selected = selected; _backend = backend;
        _generatedRoot = generatedRoot; _selectionChanged = selectionChanged;
    }

    public bool IsSiteListLoading => _isSiteListLoading;
    public string? SiteListLoadError => _siteListLoadError;
    public int SelectedSiteListCount => _selected.Count;
    public int SelectedDomainCount => _selected.Sum(item => _backend.GetDomainCount(item));

    public async Task LoadAsync()
    {
        if (_siteListsLoaded || _isSiteListLoading)
            return;

        _isSiteListLoading = true;
        _siteListLoadError = null;
        OnPropertyChanged(nameof(IsSiteListLoading));
        OnPropertyChanged(nameof(SiteListLoadError));
        try
        {
            List<SiteListElement> items = await _backend.LoadAvailableAsync();
            _available.Clear();
            foreach (SiteListElement item in items
                         .Where(item => !string.IsNullOrWhiteSpace(item.Directory))
                         .Where(item => !_backend.IsLegacyGenerated(item.Directory!))
                         .OrderBy(item => item.PackName, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                TItem siteListItem = _backend.Create(
                    item.Name ?? Path.GetFileName(item.Directory!) ?? "site-list.txt",
                    item.PackName ?? string.Empty,
                    item.Directory!, false, null, _generatedRoot());
                if (_backend.GetDomainCount(siteListItem) > 0 && !_backend.GetDisplayName(siteListItem).Contains("exclude"))
                    _available.Add(siteListItem);
            }
            _siteListsLoaded = true;
        }
        catch (Exception exception)
        {
            _siteListLoadError = exception.Message;
            OnPropertyChanged(nameof(SiteListLoadError));
        }
        finally
        {
            _isSiteListLoading = false;
            OnPropertyChanged(nameof(IsSiteListLoading));
        }
    }

    public TItem AddCustom(string filePath, string sourceName)
    {
        string fullPath = Path.GetFullPath(filePath);
        TItem? existing = _available.Concat(_selected).FirstOrDefault(item => string.Equals(
            _backend.GetFilePath(item), fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;
        return _backend.Create(Path.GetFileName(fullPath), sourceName, fullPath, false, null, _generatedRoot());
    }

    public async Task<TItem> UpdateCustomAsync(string content, string displayName, string sourceName, CancellationToken cancellationToken = default)
    {
        string normalizedContent = _backend.Normalize(content);
        if (normalizedContent.Length == 0)
            throw new InvalidDataException(_backend.Text("BlockCheck2CustomSiteListEmpty"));

        TItem? item = _selected.FirstOrDefault(candidate => _backend.IsCustom(candidate));
        string filePath = (item == null ? null : _backend.GetFilePath(item)) ?? _backend.CreateCustomPath();
        await _backend.WriteAsync(filePath, normalizedContent, cancellationToken);
        if (item == null)
        {
            item = _backend.Create(displayName, sourceName, filePath, true, normalizedContent, _generatedRoot());
            _selected.Insert(0, item);
        }
        else
        {
            _backend.UpdateCustom(item, normalizedContent);
        }
        _selectionChanged();
        return item;
    }

    public void Select(TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_selected.Any(selected => string.Equals(_backend.GetFilePath(selected), _backend.GetFilePath(item), StringComparison.OrdinalIgnoreCase)))
            return;
        _selected.Add(item);
        _selectionChanged();
    }

    public void Remove(TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_selected.Remove(item))
            _selectionChanged();
    }

    public void SetSelected(IEnumerable<TItem> selected)
    {
        _selected.Clear();
        foreach (TItem item in selected.DistinctBy(item => _backend.GetFilePath(item), StringComparer.OrdinalIgnoreCase))
            _selected.Add(item);
        _selectionChanged();
    }
}
