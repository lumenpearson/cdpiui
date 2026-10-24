using System.Collections.Generic;
using CDPIUI.Core.Store.ViewModels;

namespace CDPIUI.Views.Store;

/// <summary>
/// Lists the technical details of a Store item for the advanced information of the item page.
/// </summary>
internal static class AdditionalItemInfoBuilder
{
    internal static List<AdditionalItemInfoModel> Build(RepoItemModel item)
    {
        List<AdditionalItemInfoModel> models = [];
        models.Add(new() { DisplayName = "StoreId", DisplayValue = item.store_id });
        models.Add(new() { DisplayName = "CategoryId", DisplayValue = item.category_id });
        models.Add(new() { DisplayName = "BackgroundColor", DisplayValue = item.background });
        models.Add(new() { DisplayName = "VersionControlType", DisplayValue = item.version_control });
        if (item.version_control == "several_repos")
        {
            int i = 0;
            if (item.files_to_download != null)
            {
                foreach (var link in item.files_to_download)
                {
                    i++;
                    models.Add(new() { DisplayName = $"#{i} > Type", DisplayValue = link.type });

                    models.Add(new() { DisplayName = $"#{i} > VersionControlType", DisplayValue = link.version_control });
                    models.Add(new() { DisplayName = $"#{i} > VersionControlLink", DisplayValue = link.version_control_link });

                    models.Add(new() { DisplayName = $"#{i} > PreferredVersion", DisplayValue = link.preffered_version });
                    models.Add(new() { DisplayName = $"#{i} > PreferredFileName", DisplayValue = link.preffered_to_download_file_name });

                    models.Add(new() { DisplayName = $"#{i} > DownloadLink", DisplayValue = link.download_link });
                    models.Add(new() { DisplayName = $"#{i} > ArchiveRootFolder", DisplayValue = link.archive_root_folder });
                }
            }
        }
        else
        {
            models.Add(new() { DisplayName = "VersionControlLink", DisplayValue = item.version_control_link });
            models.Add(new() { DisplayName = "DownloadLink", DisplayValue = item.download_link });
            models.Add(new() { DisplayName = "FileType", DisplayValue = item.filetype });
            models.Add(new() { DisplayName = "ArchiveRootFolder", DisplayValue = item.archive_root_folder });
        }

        models.Add(new() { DisplayName = "TargetExecutableFile", DisplayValue = item.target_executable_file });

        models.Add(new() { DisplayName = "TargetMaxVer", DisplayValue = item.target_maxversion });
        models.Add(new() { DisplayName = "TargetMinVer", DisplayValue = item.target_minversion });
        return models;
    }
}
