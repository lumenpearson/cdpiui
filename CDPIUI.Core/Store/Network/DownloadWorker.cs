using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Shared.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CDPIUI.Core.Basic.ErrorsHelper;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;
using CDPIUI.Core.Store.MSI;

namespace CDPIUI.Core.Store.Network
{
    internal class DownloadWorker : IDisposable
    {
        private readonly RequestWorker requestWorker;
        private readonly string TempDirectory = Directories.DownloadManagerDirectory;

        private readonly CancellationToken cancellationToken;

        public readonly string OperationId;
        private string? msiGUID;

        public ErrorModel? LastError { get; private set; }

        public DownloadWorker(string operationId, CancellationToken cancellationToken, HttpClient? client = null)
        {
            this.cancellationToken = cancellationToken;

            OperationId = operationId;
            requestWorker = new RequestWorker(client);
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

            var enFiletype = filetype.ToEnum<FileExtensionTypes>(FileExtensionTypes.none);
            bool isSupportedFileExtension = enFiletype != FileExtensionTypes.none;


            string tempFileName = FileSystemService.GetNewTempFileName(nameof(DownloadWorker), FileSystemService.GetFileExtension(FileExtensionTypes.temp));
            string tempDestination = Path.Combine(TempDirectory, tempFileName);

            try
            {
                Directory.CreateDirectory(TempDirectory);

                StageChanged?.Invoke(Tuple.Create(OperationId, "Downloading"));

                Logger.Instance.CreateDebugLog(nameof(DownloadWorker), $"Uri used: {url}");

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await requestWorker.DownloadFileAsync(url, tempDestination, ReportDownloadProgress,
                        cancellationToken: cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
                {
                    string errorCode = $"ERR_DOWNLOAD_{PrettyErrorCode.UNEXPECTED_STATUS_CODE}_{(int)ex.StatusCode.Value}";
                    StageChanged?.Invoke(Tuple.Create(OperationId, "ErrorHappens"));
                    ErrorHappens?.Invoke(Tuple.Create(OperationId, errorCode, "Server Error"));
                    LastError = ErrorModel.OnlyErrorCode(errorCode);
                    return false;
                }

                // A catalogue entry with a hash pins the file: a changed release asset or a replaced
                // download is rejected before anything is extracted or installed.
                if (!string.IsNullOrWhiteSpace(expectedSha256) && !HasSha256(tempDestination, expectedSha256))
                {
                    const string errorCode = "ERR_DOWNLOAD_HASH_MISMATCH";
                    Logger.Instance.CreateWarningLog(nameof(DownloadWorker), $"The SHA-256 of {url} does not match the catalogue.");
                    StageChanged?.Invoke(Tuple.Create(OperationId, "ErrorHappens"));
                    ErrorHappens?.Invoke(Tuple.Create(OperationId, errorCode, "Hash mismatch"));
                    LastError = ErrorModel.OnlyErrorCode(errorCode);
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (extractArchive)
                {
                    StageChanged?.Invoke(Tuple.Create(OperationId, "Extracting"));
                    await ZipService.ExtractZip(tempDestination, extractRootFolder, destinationPath, extractSkipFiletypes, isCatalogCheckRequired: filetype == FileExtensionTypes.signedZip.ToString());
                }
                else
                {
                    if (!string.IsNullOrEmpty(executableFileName))
                    {
                        string copiedPath = Path.Combine(destinationPath, executableFileName + (!isSupportedFileExtension ? filetype : FileSystemService.GetFileExtension(enFiletype)));
                        File.Copy(tempDestination, copiedPath, true);
                        _extractedFiles.Add(copiedPath);
                    }
                    else
                    {
                        string exeName = string.IsNullOrEmpty(filename) ? GetFileNameFromUri(url) : filename;
                        if (string.IsNullOrEmpty(exeName))
                            throw new IOException();

                        string extension = !isSupportedFileExtension? filetype : FileSystemService.GetFileExtension(enFiletype);

                        string copiedPath = Path.Combine(destinationPath, exeName + extension);
                        File.Copy(tempDestination, copiedPath, true);
                        _extractedFiles.Add(copiedPath);
                    }
                }

                if (filetype == FileExtensionTypes.msi.ToString() || filetype == FileExtensionTypes.elmsi.ToString())
                {
                    StageChanged?.Invoke(Tuple.Create(OperationId, "ConnectingToService"));
                    // The installer is the file copied above, whose name can come from
                    // target_executable_file and not from the URL.
                    if (_extractedFiles.Count == 0)
                        throw new IOException("The installer was not copied.");
                    string msiPath = _extractedFiles[^1];
                    msiGUID = Guid.NewGuid().ToString();
                    MsiInstallerService msiInstallerHelper = new(msiGUID, msiPath);
                    msiInstallerHelper.callbackAction += HandleMsiInstallerMessage;
                    MsiCallback callback = await msiInstallerHelper.Run(cancellationToken);
                    msiInstallerHelper.callbackAction -= HandleMsiInstallerMessage;

                    Logger.Instance.CreateDebugLog(nameof(DownloadWorker), "TRY");

                    if (callback.State == MsiState.ExceptionHappens)
                    {
                        success = false;
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


                cancellationToken.ThrowIfCancellationRequested();
                StageChanged?.Invoke(Tuple.Create(OperationId, "Completed"));
                success = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is a user action, not a download failure.
            }
            catch (Exception ex) 
            {
                HandleError(ex);
            }
            finally
            {
                try { if (File.Exists(tempDestination)) File.Delete(tempDestination); }
                catch (Exception ex) { Logger.Instance.CreateWarningLog(nameof(DownloadWorker), $"Cannot remove the temporary file \"{tempDestination}\": {ex.Message}"); }

                // An installer copied from this download is removed again when it failed to install
                // only; other files may have replaced the files of the installed version and
                // stay. Archives of signed packs are checked in a separate folder by ZipService and
                // never reach the destination unchecked.
                bool isInstaller = filetype == FileExtensionTypes.msi.ToString() || filetype == FileExtensionTypes.elmsi.ToString();
                if (removeAfterAction || (!success && isInstaller))
                {
                    foreach (var file in _extractedFiles)
                    {
                        try { if (File.Exists(file)) File.Delete(file); }
                        catch (Exception ex) { Logger.Instance.CreateWarningLog(nameof(DownloadWorker), $"Cannot remove \"{file}\": {ex.Message}"); }
                    }
                }
            }
            return success;
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
            if (msiGUID == callback.operationId)
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
        public void Dispose()
        {
            requestWorker.Dispose();
        }
    }
}
