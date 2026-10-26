using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Core.System;
using CDPIUI.Helper.AddOns.BlockCheck2;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2MainPageViewModel
{
    public void SelectSiteList(BlockCheck2SiteListItem item)
    {
        Session.SelectSiteList(item);
        IsInputNoticeOpen = false;
        UpdateNavigationButtons();
    }

    public void RemoveSiteList(BlockCheck2SiteListItem item)
    {
        Session.RemoveSiteList(item);
        UpdateNavigationButtons();
    }

    public void OpenSiteListFile(BlockCheck2SiteListItem item) =>
        ShellHelper.OpenFileInDefaultApp(item.FilePath);

    /// <summary>Stores the custom site list the user entered; a failure is shown as an input issue.</summary>
    public async Task ApplyCustomSiteListAsync(string content)
    {
        try
        {
            await Session.UpdateCustomSiteListAsync(
                content,
                Text("BlockCheck2CustomSiteListName"),
                Text("BlockCheck2LocalSiteListSource"));
            IsInputNoticeOpen = false;
            UpdateNavigationButtons();
        }
        catch (Exception exception)
        {
            ShowInputIssue(exception.Message);
        }
    }

    /// <summary>Imports a chosen site list file. Returns <see langword="true"/> when it was added.</summary>
    public async Task<bool> ImportSiteListFileAsync(string filePath, string displayName)
    {
        try
        {
            if (!await TryImportSiteListFileAsync(filePath, displayName))
            {
                ShowInputNotice(
                    Text("BlockCheck2SiteListInvalidTitle"),
                    Text("BlockCheck2SiteListInvalidMessage"));
                return false;
            }
        }
        catch (Exception exception)
        {
            ShowSiteListLoadFailed(exception.Message);
            return false;
        }

        IsInputNoticeOpen = false;
        UpdateNavigationButtons();
        return true;
    }

    public void ShowSiteListLoadFailed(string message) =>
        ShowInputNotice(Text("BlockCheck2SiteListsLoadFailedTitle"), message);

    /// <summary>
    /// Imports dropped text files in order. <paramref name="skipped"/> counts the dropped items
    /// that are not text files.
    /// </summary>
    public async Task ImportDroppedSiteListsAsync(
        IReadOnlyList<(string Path, string Name)> files,
        int skipped)
    {
        int imported = 0;
        List<string> errors = [];
        foreach ((string path, string name) in files)
        {
            try
            {
                if (await TryImportSiteListFileAsync(path, name))
                {
                    imported++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception exception)
            {
                errors.Add($"{name}: {exception.Message}");
            }
        }

        UpdateNavigationButtons();
        if (skipped > 0 || errors.Count > 0)
        {
            ShowInputNotice(
                Text("BlockCheck2SiteListDropPartialTitle"),
                string.Format(
                    Text("BlockCheck2SiteListDropPartialFormat"),
                    imported,
                    skipped + errors.Count,
                    errors.Count == 0
                        ? string.Empty
                        : Environment.NewLine + string.Join(Environment.NewLine, errors.Take(3))));
        }
        else
        {
            IsInputNoticeOpen = false;
        }
    }

    private async Task<bool> TryImportSiteListFileAsync(
        string filePath,
        string displayName)
    {
        string? storedPath = await BlockCheck2SiteListImporter.TryImportAsync(filePath, displayName);
        if (storedPath == null)
        {
            return false;
        }

        BlockCheck2SiteListItem item = Session.AddCustomSiteList(
            storedPath,
            Text("BlockCheck2LocalSiteListSource"));
        Session.SelectSiteList(item);
        return true;
    }
}
