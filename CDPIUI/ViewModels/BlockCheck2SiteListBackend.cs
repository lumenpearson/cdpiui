#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.GoodCheck;
using WinUI3Localizer;

namespace CDPIUI.ViewModels;

internal sealed class BlockCheck2SiteListBackend : IBlockCheck2SiteListBackend<BlockCheck2SiteListItem>
{
    public Task<List<SiteListElement>> LoadAvailableAsync() => SiteListHelper.GetAllAvailableSiteListTemplatesAsync();
    public bool IsLegacyGenerated(string path) => BlockCheck2SiteListStorage.IsLegacyGeneratedSiteList(path);
    public BlockCheck2SiteListItem Create(string displayName, string sourceName, string path, bool isCustom, string? editableContent, string root) =>
        new(displayName, sourceName, path, isCustom, editableContent, individualSiteListRoot: root);
    public string Normalize(string content) => BlockCheck2SiteListStorage.NormalizeCustomContent(content);
    public string CreateCustomPath() => BlockCheck2SiteListStorage.CreateCustomSiteListPath();
    public Task WriteAsync(string path, string normalized, CancellationToken token) => BlockCheck2SiteListStorage.WriteCustomSiteListAsync(path, normalized, token);
    public string Text(string key) => Localizer.Get().GetLocalizedString(key);
    public string GetFilePath(BlockCheck2SiteListItem item) => item.FilePath;
    public string GetDisplayName(BlockCheck2SiteListItem item) => item.DisplayName;
    public int GetDomainCount(BlockCheck2SiteListItem item) => item.DomainCount;
    public bool IsCustom(BlockCheck2SiteListItem item) => item.IsCustom;
    public void UpdateCustom(BlockCheck2SiteListItem item, string content) => item.UpdateCustomContent(content);
}
