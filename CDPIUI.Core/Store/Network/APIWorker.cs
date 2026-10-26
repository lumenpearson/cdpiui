using System.Diagnostics;
using System.Text.Json;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Network.Models;
using CDPIUI.Core.Store.Repository;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Shared.Secrets;
namespace CDPIUI.Core.Store.Network;

public interface IAPIWorker
{
    /// <summary>
    /// Get latest version and release notes for URL and current
    /// version control type
    /// </summary>
    /// <param name="url">Url to check</param>
    /// <returns>Latest release info</returns>
    Task<OperationResultModel<ReleaseInfoModel>> GetLastVersionAndVersionNotes(string url);
}

internal class APIWorker : IAPIWorker
{
    public string? Token;
    public SupportedVersionControls VersionControl;

    public async Task<OperationResultModel<APILinkModel>> GetDownloadLinkForVersion(
        string? repoUrl,
        string? targetFileOrFileType,
        string? version = null,
        string? preferredFile = null
        )
    {
        APILinkModel linkModel = new();

        try
        {
            if (string.IsNullOrEmpty(repoUrl))
                throw new ArgumentNullException(nameof(repoUrl));
            if (string.IsNullOrEmpty(targetFileOrFileType))
                throw new ArgumentNullException(nameof(targetFileOrFileType));


            string response = await GetGithubResponse(repoUrl, version);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            linkModel.version = root.GetProperty("tag_name").GetString();

            var assets = GetAssetsForVersionControl(root, VersionControl);
            if (assets == null)
            {
                return OperationResultModel<APILinkModel>
                    .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.ERROR_HTTP_INVALID_SERVER_RESPONSE));
            }

            // The value is a file type or an exact asset name. A name is not a type, so it
            // matches by name only.
            var fileType = targetFileOrFileType.ToEnum(FileExtensionTypes.none);
            string? typeExtension = fileType == FileExtensionTypes.none ? null : FileSystemService.GetFileExtension(fileType);

            var matches = ((JsonElement.ArrayEnumerator)assets)
                .Select(a => new
                {
                    Name = TryGetValue(a, "name"),
                    Url = GetDownloadUrlForVersionControl(a, VersionControl)
                })
                .Where(a =>
                    !string.IsNullOrEmpty(a.Name)
                )
                .Where(a =>
                    string.Equals(a.Name, targetFileOrFileType, StringComparison.OrdinalIgnoreCase)
                    || (typeExtension != null && a.Name.EndsWith(typeExtension, StringComparison.OrdinalIgnoreCase))
                )
                .Where(a =>
                    preferredFile is null ||
                    a.Name.Contains(preferredFile, StringComparison.Ordinal)
                )
                .ToList();

            if (matches.Count == 0)
            {
                return OperationResultModel<APILinkModel>
                    .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.INVALID_URI));
            }

            // FIX: Possible issue on update process.
            if (matches.Count > 1)
            {
                return OperationResultModel<APILinkModel>
                    .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.TOO_MANY_VARIANTS));
            }

            linkModel.link = matches[0].Url;

            return OperationResultModel<APILinkModel>.SuccessResult(linkModel);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(APIWorker), ex.Message);
            return OperationResultModel<APILinkModel>
                    .FailureResult(ErrorsHelper.Convertor.GetErrorModel(nameof(APIWorker), ex));
        }
    }

    public async Task<OperationResultModel<List<DownloadLinkModel>>> GetDownloadLinksAsync(List<FileToDownload>? filesToDownload)
    {
        List<DownloadLinkModel> links = [];

        if (filesToDownload == null)
            return OperationResultModel<List<DownloadLinkModel>>
                .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));

        foreach (var file in filesToDownload)
        {
            string? downloadUrl, downloadVersion;
            if (file.version_control == "external_site_only_last")
            {
                downloadUrl = file.download_link;
                downloadVersion = null;
            }
            else
            {
                if (file.version_control_link is null || file.type is null)
                    return OperationResultModel<List<DownloadLinkModel>>
                        .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));

                var result =
                    await GetDownloadLinkForVersion(
                        file.version_control_link,
                        file.type,
                        file.preffered_version,
                        file.preffered_to_download_file_name
                        );

                if (result.ErrorHappens)
                {
                    return OperationResultModel<List<DownloadLinkModel>>
                        .FailureResult(result.Error!);
                }
                else
                {
                    downloadUrl = result.Result.link;
                    downloadVersion = result.Result.version;
                }
            }

            links.Add(new()
            {
                link = downloadUrl,
                version = downloadVersion,
                type = file.type,
                archive_root_folder = file.archive_root_folder,
                actions = file.actions,
                target_executable_file = null,
                sha256 = file.sha256
            });
        }

        return OperationResultModel<List<DownloadLinkModel>>
            .SuccessResult(links);
    }

    public async Task<OperationResultModel<ReleaseInfoModel>> GetLastVersionAndVersionNotes(string repoUrl)
    {
        string notes;
        string tag;
        try
        {
            string response = await GetGithubResponse(repoUrl, null);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            tag = root.GetProperty("tag_name").GetString();
            notes = GetReleaseNotesForVersionControl(root, VersionControl);

            return OperationResultModel<ReleaseInfoModel>
                .SuccessResult(ReleaseInfoModel.BasicReleaseInfo(tag, notes));
        }
        catch (Exception ex)
        {
            return OperationResultModel<ReleaseInfoModel>
                .FailureResult(ErrorsHelper.Convertor.GetErrorModel(nameof(GetLastVersionAndVersionNotes), ex));
        }
    }


    private async Task<string> GetGithubResponse(
        string repoUrl,
        string? version)
    {
        // Without a token the request is anonymous: public repositories work with the
        // lower rate limit, and private ones return an HTTP error.
        var uri = new Uri(repoUrl);
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // GitLab projects may sit in nested groups ("group/subgroup/project"), and its pages
        // continue after "/-/".
        if (VersionControl == SupportedVersionControls.GitLab)
        {
            int dash = Array.IndexOf(parts, "-");
            if (dash >= 0)
                parts = parts[..dash];
        }
        else if (parts.Length > 2)
        {
            parts = parts[..2];
        }
        if (parts.Length < 2)
            throw new ArgumentException("Invalid repository URL.", nameof(repoUrl));
        var owner = string.Join("/", parts[..^1]);
        var repo = parts[^1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];


        string apiUrl = GetApiUrlForVersion(owner, repo, version, VersionControl);


        using var requestWorker = new RequestWorker();
        var response = await requestWorker.GetStringAsync(apiUrl, Token, "token", useStoreUserAgent: true);
        Logger.Instance.CreateDebugLog(nameof(APIWorker), version ?? "Version not provided");
        Logger.Instance.CreateDebugLog(nameof(APIWorker), apiUrl);

        return response;
    }

    private static string GetApiUrlForVersion(string owner, string repo, string? version, SupportedVersionControls versionControl)
    {
        string url = string.Empty;
        // The version comes from item metadata; escaped, "../" cannot move the request to another API endpoint.
        if (!string.IsNullOrEmpty(version))
            version = Uri.EscapeDataString(version);
        if (versionControl == SupportedVersionControls.GitLab)
        {
            // The project id is the whole path with every '/' encoded.
            string project = Uri.EscapeDataString($"{owner}/{repo}");
            return string.IsNullOrEmpty(version)
                ? $"https://gitlab.com/api/v4/projects/{project}/releases/permalink/latest"
                : $"https://gitlab.com/api/v4/projects/{project}/releases/{version}";
        }
        owner = Uri.EscapeDataString(owner);
        repo = Uri.EscapeDataString(repo);
        if (versionControl == SupportedVersionControls.GitHub)
        {
            url = string.IsNullOrEmpty(version)
                ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
                : $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{version}";
        }
        return url;
    }



    private static JsonElement.ArrayEnumerator? GetAssetsForVersionControl(JsonElement root, SupportedVersionControls versionControl)
    {
        return versionControl switch
        {
            SupportedVersionControls.GitHub => (JsonElement.ArrayEnumerator?)root.GetProperty("assets").EnumerateArray(),
            SupportedVersionControls.GitLab => (JsonElement.ArrayEnumerator?)root.GetProperty("assets").GetProperty("links").EnumerateArray(),
            SupportedVersionControls.None => null,
            _ => null,
        };
    }

    private static string GetDownloadUrlForVersionControl(JsonElement root, SupportedVersionControls versionControl)
    {
        return versionControl switch
        {
            SupportedVersionControls.GitHub => root.GetProperty("browser_download_url").GetString() ?? string.Empty,
            SupportedVersionControls.GitLab => root.GetProperty("url").GetString() ?? string.Empty,
            SupportedVersionControls.None => string.Empty,
            _ => string.Empty,
        };
    }

    private static string GetReleaseNotesForVersionControl(JsonElement root, SupportedVersionControls versionControl)
    {
        return versionControl switch
        {
            SupportedVersionControls.GitHub => root.GetProperty("body").GetString() ?? string.Empty,
            SupportedVersionControls.GitLab => root.GetProperty("description").GetString() ?? string.Empty,
            SupportedVersionControls.None => string.Empty,
            _ => string.Empty,
        };
    }

    private static string TryGetValue(JsonElement jsonElement, string key)
    {
        if (jsonElement.TryGetProperty(key, out var value))
        {
            return value.GetString() ?? string.Empty;
        }
        return string.Empty;
    }
}
