#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.GoodCheck;

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2SiteListBackend<TItem> where TItem : class
{
    Task<List<SiteListElement>> LoadAvailableAsync();
    bool IsLegacyGenerated(string path);
    TItem Create(string displayName, string sourceName, string path, bool isCustom, string? editableContent, string root);
    string Normalize(string content);
    string CreateCustomPath();
    Task WriteAsync(string path, string normalized, CancellationToken token);
    string Text(string key);
    string GetFilePath(TItem item);
    string GetDisplayName(TItem item);
    int GetDomainCount(TItem item);
    bool IsCustom(TItem item);
    void UpdateCustom(TItem item, string content);
}
