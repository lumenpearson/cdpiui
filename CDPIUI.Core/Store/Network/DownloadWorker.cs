using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.MSI;
using CDPIUI.Core.System;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.PrettyErrorConversionService;
using static CDPIUI.Core.Basic.ErrorsHelper;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;

namespace CDPIUI.Core.Store.Network;

internal class DownloadWorker : IDisposable
{
    private readonly RequestWorker _requestWorker;
    private readonly string _tempDirectory = Directories.DownloadManagerDirectory;

    private readonly CancellationToken _cancellationToken;

    public readonly string OperationId;
    private string? _msiGUID;

    public ErrorModel? LastError { get; private set; }

    public DownloadWorker(string operationId, CancellationToken cancellationToken, HttpClient? client = null)
    {
        _cancellationToken = cancellationToken;

        OperationId = operationId;
        _requestWorker = new RequestWorker(client);
    }

    public event Action<Tuple<string, double>>? DownloadSpeedChanged;
    public event Action<Tuple<string, double>>? ProgressChanged;
    public event Action<Tuple<string, TimeSpan>>? TimeRemainingChanged;
    public event Action<Tuple<string, string>>? StageChanged; // Downloading, Extracting, Completed, ErrorHappens
    public event Action<Tuple<string, string, string>>? ErrorHappens;

    public bool IsRestartNeeded = false;

    public async Task<bool> DownloadAndExtractAsync(
        string url,
        string destinationPath,
        bool extractArchive = false,
        IEnumerable<string>? extractSkipFiletypes = null,
        string? extractRootFolder = null,
        string? executableFileName = "executableFile",
        string filetype = "",
        bool removeAfterAction = false,
        string filename = "",
        string? expectedSha256 = null
    )
    {
        LastError = null;
        IsRestartNeeded = false;
        bool success = false;
        List<string> _extractedFiles = [];

        string tempFileName = FileSystemService.GetNewTempFileName(nameof(DownloadWorker), FileSystemService.GetFileExtension(FileExtensionTypes.temp));
        string tempDestination = Path.Combine(_tempDirectory, tempFileName);

        try
        {
            if (!await DownloadToTempFileAsync(url, tempDestination, expectedSha256))
                return false;

            _cancellationToken.ThrowIfCancellationRequested();
            if (extractArchive)
            {
                StageChanged?.Invoke(Tuple.Create(OperationId, "Extracting"));
                await ZipService.ExtractZip(tempDestination, extractRootFolder, destinationPath, extractSkipFiletypes, isCatalogCheckRequired: filetype == FileExtensionTypes.signedZip.ToString());
            }
            else
            {
                _extractedFiles.Add(CopyDownloadedFile(tempDestination, destinationPath, url, executableFileName, filetype, filename));
            }

            if (IsInstallerFiletype(filetype))
            {
                // The installer is the file copied above, whose name can come from
                // target_executable_file and not from the URL.
                StageChanged?.Invoke(Tuple.Create(OperationId, "ConnectingToService"));
                if (_extractedFiles.Count == 0)
                    throw new IOException("The installer was not copied.");
                await InstallMsiAsync(_extractedFiles[^1], removeAfterAction);
            }

            _cancellationToken.ThrowIfCancellationRequested();
            StageChanged?.Invoke(Tuple.Create(OperationId, "Completed"));
            success = true;
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            // Cancellation is a user action, not a download failure.
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
        finally
        {
            CleanUp(tempDestination, _extractedFiles, removeOutputs: removeAfterAction || (!success && IsInstallerFiletype(filetype)));
        }
        return success;
    }

    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="tempDestination"/>. Returns false, with
    /// the error reported, when the server answers with an error status or the hash does not match.
    /// </summary>
    private async Task<bool> DownloadToTempFileAsync(string url, string tempDestination, string? expectedSha256)
    {
        Directory.CreateDirectory(_tempDirectory);

        StageChanged?.Invoke(Tuple.Create(OperationId, "Downloading"));

        Logger.Instance.CreateDebugLog(nameof(DownloadWorker), $"Uri used: {url}");

        _cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _requestWorker.DownloadFileAsync(url, tempDestination, ReportDownloadProgress,
                cancellationToken: _cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            ReportFailure($"ERR_DOWNLOAD_{PrettyErrorCode.UNEXPECTED_STATUS_CODE}_{(int)ex.StatusCode.Value}", "Server Error");
            return false;
        }

        // A catalogue entry with a hash pins the file: a changed release asset or a replaced
        // download is rejected before anything is extracted or installed.
        if (!string.IsNullOrWhiteSpace(expectedSha256) && !HasSha256(tempDestination, expectedSha256))
        {
            Logger.Instance.CreateWarningLog(nameof(DownloadWorker), $"The SHA-256 of {url} does not match the catalogue.");
            ReportFailure("ERR_DOWNLOAD_HASH_MISMATCH", "Hash mismatch");
            return false;
        }
        return true;
    }

    private string CopyDownloadedFile(
        string tempDestination,
        string destinationPath,
        string url,
        string? executableFileName,
        string filetype,
        string filename
    )
    {
        var enFiletype = filetype.ToEnum<FileExtensionTypes>(FileExtensionTypes.none);
        string extension = enFiletype == FileExtensionTypes.none ? filetype : FileSystemService.GetFileExtension(enFiletype);

        string copiedPath;
        if (!string.IsNullOrEmpty(executableFileName))
        {
            // target_executable_file comes from the catalogue: "..\..\x" wrote the
            // download outside the item folder.
            if (!IsPlainFileName(executableFileName))
                throw new InvalidDataException($"Invalid target file name: {executableFileName}");
            copiedPath = Path.Combine(destinationPath, executableFileName + extension);
        }
        else
        {
            string exeName = string.IsNullOrEmpty(filename) ? GetFileNameFromUri(url) : filename;
            if (string.IsNullOrEmpty(exeName))
                throw new IOException();

            copiedPath = Path.Combine(destinationPath, exeName + extension);
        }
        File.Copy(tempDestination, copiedPath, true);
        return copiedPath;
    }

    private async Task InstallMsiAsync(string msiPath, bool removeAfterAction)
    {
        _msiGUID = Guid.NewGuid().ToString();
        MsiInstallerService msiInstallerHelper = new(_msiGUID, msiPath);
        msiInstallerHelper.callbackAction += HandleMsiInstallerMessage;
        MsiCallback callback = await msiInstallerHelper.Run(_cancellationToken);
        msiInstallerHelper.callbackAction -= HandleMsiInstallerMessage;

        Logger.Instance.CreateDebugLog(nameof(DownloadWorker), "TRY");

        if (callback.State == MsiState.ExceptionHappens)
        {
            throw new MsiInstallException("MSI_UNKNOWN");
        }
        else if (callback.State == MsiState.CompleteRestartRequest)
        {
            IsRestartNeeded = true;
        }

        if (removeAfterAction)
        {
            File.Delete(msiPath);
        }
    }

    private static bool IsInstallerFiletype(string filetype) =>
        filetype == FileExtensionTypes.msi.ToString() || filetype == FileExtensionTypes.elmsi.ToString();

    /// <param name="removeOutputs">
    /// An installer copied from this download is removed again when it failed to install
    /// only; other files may have replaced the files of the installed version and
    /// stay. Archives of signed packs are checked in a separate folder by ZipService and
    /// never reach the destination unchecked.
    /// </param>
    private static void CleanUp(string tempDestination, List<string> copiedFiles, bool removeOutputs)
    {
        try
        { if (File.Exists(tempDestination)) File.Delete(tempDestination); }
        catch (Exception ex) { Logger.Instance.CreateWarningLog(nameof(DownloadWorker), $"Cannot remove the temporary file \"{tempDestination}\": {ex.Message}"); }

        if (!removeOutputs)
            return;

        foreach (var file in copiedFiles)
        {
            try
            { if (File.Exists(file)) File.Delete(file); }
            catch (Exception ex) { Logger.Instance.CreateWarningLog(nameof(DownloadWorker), $"Cannot remove \"{file}\": {ex.Message}"); }
        }
    }

    public async Task<bool> DownloadAndExtractAsync(
        string url,
        string destinationPath,
        bool extractArchive = false,
        IEnumerable<string>? extractSkipFiletypes = null,
        string? extractRootFolder = null,
        string? executableFileName = "executableFile",
        FileExtensionTypes filetype = FileExtensionTypes.temp,
        bool removeAfterAction = false,
        string filename = "",
        string? expectedSha256 = null
    )
    {
        return await DownloadAndExtractAsync(url, destinationPath, extractArchive, extractSkipFiletypes, extractRootFolder, executableFileName, filetype.ToString(), removeAfterAction, filename, expectedSha256);
    }

    internal static bool IsPlainFileName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name == Path.GetFileName(name) &&
        name != "." && name != ".." &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !name.Contains('\\') && !name.Contains('/');

    internal static bool HasSha256(string file, string expectedSha256)
    {
        string expected = new string(expectedSha256.Where(Uri.IsHexDigit).ToArray());
        using var stream = File.OpenRead(file);
        string actual = Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(stream));
        return expected.Length == 64 && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private void ReportDownloadProgress(long totalRead, long totalBytes, TimeSpan elapsed)
    {
        var speed = totalRead / elapsed.TotalSeconds;
        DownloadSpeedChanged?.Invoke(Tuple.Create(OperationId, speed));

        if (totalBytes != -1)
        {
            var progress = (double)totalRead / totalBytes * 100;
            ProgressChanged?.Invoke(Tuple.Create(OperationId, progress));

            var timeRemaining = TimeSpan.FromSeconds((totalBytes - totalRead) / speed);
            TimeRemainingChanged?.Invoke(Tuple.Create(OperationId, timeRemaining));
        }
    }




    public void HandleMsiInstallerMessage(MsiCallback callback)
    {
        if (_msiGUID == callback.operationId)
        {
            StageChanged?.Invoke(Tuple.Create(OperationId, callback.State.ToString()));
        }
    }

    private string GetFileNameFromUri(string _uri)
    {
        var uri = new Uri(_uri);

        string path = uri.AbsolutePath.TrimEnd('/');
        string fileName = Path.GetFileNameWithoutExtension(path);
        return fileName;
    }

    private void HandleError(Exception ex)
    {
        StageChanged?.Invoke(Tuple.Create(OperationId, "ErrorHappens"));
        string errorCode = Convertor.GetPrettyErrorCode("ERR_NET_DOWNLOAD", ex);
        ErrorHappens?.Invoke(Tuple.Create(OperationId, errorCode, $"{ex}"));
        LastError = ErrorModel.OnlyErrorCode(errorCode);
    }

    private void ReportFailure(string errorCode, string message)
    {
        StageChanged?.Invoke(Tuple.Create(OperationId, "ErrorHappens"));
        ErrorHappens?.Invoke(Tuple.Create(OperationId, errorCode, message));
        LastError = ErrorModel.OnlyErrorCode(errorCode);
    }
    public void Dispose()
    {
        _requestWorker.Dispose();
    }
}
