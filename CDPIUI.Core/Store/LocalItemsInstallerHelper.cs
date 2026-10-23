using CDPIUI.Core.Basic;
using CDPIUI.Core.JSON;
using CDPIUI.Core.LScript;
using CDPIUI.Core.System;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Core.Store
{
    public class LocalItemInitModel
    {
        public string? StoreId { get; set; }
        public string? Type { get; set; }
        public string? Version { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? ReadyToUseIcon = null;
        public List<string[]>? Requirements { get; set; }
        public string? ShortName { get; set; }
        public string? Color { get; set; }
        public string? Developer { get; set; }
        public string? BeforeInstallActions { get; set; }
        public string? AfterInstallActions { get; set; }
        public string? ExecutableFile { get; set; }
    }

    public class LocalItemsInstallerHelper
    {
        private readonly string TempDirectory;

        private const string AppTempDirectory = "TempFiles";
        private const string DownloadManagerDirectory = "Offline";

        public Action<string>? ErrorHappens;

        private static LocalItemsInstallerHelper? _instance;
        private static readonly object _lock = new object();

        public static LocalItemsInstallerHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new LocalItemsInstallerHelper();
                    return _instance;
                }
            }
        }

        // Files an unsigned pack may not bring: programs, libraries, drivers, installers and scripts.
        private static readonly List<string> UnsignedPackSkippedFiles =
        [
            "*.exe", "*.dll", "*.sys", "*.msi", "*.com", "*.scr", "*.cpl", "*.bat", "*.cmd",
            "*.ps1", "*.vbs", "*.vbe", "*.js", "*.jse", "*.wsf", "*.hta", "*.lnk", "*.url",
        ];

        private LocalItemsInstallerHelper() 
        {
            string localAppData = CDPIUI.Core.Data.Directories.DataDirectory;
            TempDirectory = Path.Combine(localAppData, AppTempDirectory, DownloadManagerDirectory);
        }

        public async Task<LocalItemInitModel?> ImportStoreItemPackFile(string itemPackFile, Action<string>? onError = null)
        {
            string tempFolderName = FileSystemService.GetNewTempFileName("ap");
            string tempDestination = Path.Combine(TempDirectory, tempFolderName);

            bool isCatalogCheckRequired = Path.GetExtension(itemPackFile) == ".cdpisignedpack" ? true : false;
            List<string> filesToSkip = isCatalogCheckRequired ? [] : UnsignedPackSkippedFiles;

            try
            {
                await ZipService.ExtractZip(itemPackFile, "/", tempDestination, filesToSkip: filesToSkip, isCatalogCheckRequired: isCatalogCheckRequired);
                string initFilePath = Path.Combine(tempDestination, "init.json");

                if (!File.Exists(initFilePath)) 
                {
                    string error = "ERR_LOCAL_ITEM_UNSUPPORTED";
                    (onError ?? ErrorHappens)?.Invoke(error);
                    return null;
                }
                LocalItemInitModel localItemInitModel = JSONConvertor.LoadJson<LocalItemInitModel>(initFilePath);

                if (localItemInitModel.Icon?.StartsWith("$DYNAMICIMAGE") == true)
                {
                    string iconUrl = LScriptCore.ExecuteScript(localItemInitModel.Icon, scriptArgs: tempDestination);
                    // Only an image of the pack itself: an absolute path in Icon copied any local file
                    // before the user had confirmed anything.
                    if (File.Exists(iconUrl) && IsPackImage(tempDestination, iconUrl))
                    {
                        string newIconUrl = Path.Combine(TempDirectory, $"{tempFolderName}_ICONDATA", Path.GetFileName(iconUrl));

                        Directory.CreateDirectory(Path.GetDirectoryName(newIconUrl)!);

                        File.Copy(iconUrl, newIconUrl, overwrite: true);
                        localItemInitModel.ReadyToUseIcon = newIconUrl;
                    }
                }

                if (localItemInitModel.StoreId == SharedConstants.ApplicationStoreId || localItemInitModel.StoreId == SharedConstants.LocalUserItemsId)
                {
                    throw new AccessViolationException("StoreId is protected by security policy");
                }
                if (!StoreItemIds.IsValid(localItemInitModel.StoreId))
                {
                    throw new InvalidDataException("StoreId is not a valid identifier");
                }

                Directory.Delete(tempDestination, recursive: true);

                if (!isCatalogCheckRequired) localItemInitModel.ExecutableFile = null;
                return localItemInitModel;
            }
            catch (Exception ex)
            {
                string error = HandleError(ex);
                (onError ?? ErrorHappens)?.Invoke(error);
                return null;
            }
        }

        public async Task BeginLocalItemInstalling(string itemPackFile, Action<string>? onError = null, CancellationToken cancellationToken = default)
        {
            string tempFolderName = FileSystemService.GetNewTempFileName("ap");
            string tempDestination = Path.Combine(TempDirectory, tempFolderName);

            bool isCatalogCheckRequired = Path.GetExtension(itemPackFile) == ".cdpisignedpack" ? true : false;
            List<string> filesToSkip = isCatalogCheckRequired ? [] : UnsignedPackSkippedFiles;

            try
            {
                await ZipService.ExtractZip(itemPackFile, "/", tempDestination, filesToSkip: filesToSkip, isCatalogCheckRequired: isCatalogCheckRequired);
                string initFilePath = Path.Combine(tempDestination, "init.json");

                if (!File.Exists(initFilePath))
                {
                    string error = "ERR_LOCAL_ITEM_UNSUPPORTED";
                    (onError ?? ErrorHappens)?.Invoke(error);
                    return;
                }
                LocalItemInitModel localItemInitModel = JSONConvertor.LoadJson<LocalItemInitModel>(initFilePath);
                if (!StoreItemIds.IsValid(localItemInitModel.StoreId) ||
                    localItemInitModel.StoreId == SharedConstants.ApplicationStoreId ||
                    localItemInitModel.StoreId == SharedConstants.LocalUserItemsId)
                {
                    throw new InvalidDataException("StoreId is not allowed for a local pack");
                }

                if (cancellationToken.IsCancellationRequested) return;
                StoreHelper.Instance.AddItemToQueue(localItemInitModel.StoreId!, packFile: tempDestination, isSignedPack: isCatalogCheckRequired);
                return;
            }
            catch (Exception ex)
            {
                string error = HandleError(ex);
                (onError ?? ErrorHappens)?.Invoke(error);
                return;
            }
        }

        public async void ImportApplicationUpdatePatchFromFile(string path)
        {

            string tempFolderName = FileSystemService.GetNewTempFileName("ap");
            string tempDestination = Path.Combine(TempDirectory, tempFolderName);

            bool isCatalogCheckRequired = Path.GetExtension(path) == ".cdpisignedpack" ? true : false;
            List<string> filesToSkip = isCatalogCheckRequired ? [] : UnsignedPackSkippedFiles;

            try
            {
                await ZipService.ExtractZip(path, "/", tempDestination, filesToSkip: filesToSkip, isCatalogCheckRequired: isCatalogCheckRequired);
            }
            catch (Exception ex)
            {
                string error = HandleError(ex);
            }

        }

        private string HandleError(Exception ex)
        {
            return ErrorsHelper.Convertor.GetPrettyErrorCode("ERR_LOCAL_ITEM_INSTALLING", ex);
        }

        private static readonly string[] PackImageExtensions = [".png", ".jpg", ".jpeg", ".ico", ".bmp", ".gif"];

        private static bool IsPackImage(string packDirectory, string file)
        {
            string root = Path.GetFullPath(packDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(file);
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                PackImageExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase);
        }
    }
}
