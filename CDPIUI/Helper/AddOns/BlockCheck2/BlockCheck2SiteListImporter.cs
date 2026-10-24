using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Core.Data;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

/// <summary>
/// Imports a site list file the user picked or dropped: checks that it has at least one valid
/// target and copies it into the local user items, unless it is already stored there.
/// </summary>
internal static class BlockCheck2SiteListImporter
{
    /// <summary>
    /// Returns the path of the stored copy, or <see langword="null"/> when the file has no
    /// valid targets.
    /// </summary>
    public static async Task<string?> TryImportAsync(string filePath, string displayName)
    {
        BlockCheckSiteListLoadResult validation = await Task.Run(() =>
            new BlockCheckSiteListLoader().Load(
                [new BlockCheckSiteListInput(displayName, filePath)],
                new BlockCheckTargetInputOptions
                {
                    Protocols = new HashSet<BlockCheckProtocol> { BlockCheckProtocol.TlsAuto },
                    IpVersions = new HashSet<BlockCheckIpVersion> { BlockCheckIpVersion.IPv4 },
                }));
        bool valid = validation.Targets.Count > 0 &&
                     validation.Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
        if (!valid)
        {
            return null;
        }

        string targetFolder = Path.Combine(
            Directories.StoreItemsDirectory,
            SharedConstants.LocalUserItemsId,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "Imported");
        string sourcePath = Path.GetFullPath(filePath);
        string targetRoot = Path.GetFullPath(targetFolder)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return sourcePath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase)
            ? sourcePath
            : FileSystemService.CopyTxtWithUniqueName(sourcePath, targetFolder);
    }
}
