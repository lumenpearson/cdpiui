using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Security;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.System;
using CDPIUI.Shared;
using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.Exceptions.Catalog;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;
using System.IO.Compression;

namespace CDPIUI.Core.Store.Application
{
    internal class ApplicationUpdateService
    {
        public async Task<OperationResultModel<EmptyResult>> GetPatchReadyToInstall(
            string filePath,
            DownloadWorker? downloadWorker)
        {
            string appItemDirectory = Path.Combine(Directories.StoreItemsDirectory, SharedConstants.ApplicationStoreId);

            string patchesDirectory = Path.Combine(appItemDirectory, "Patches");

            string patchDirectory = Path.Combine(
                patchesDirectory,
                Path.GetFileNameWithoutExtension(filePath));

            try
            {
                await UnpackPatch(filePath, patchDirectory);

                var requirementsResult = LoadPatchRequirements(patchDirectory);
                if (!requirementsResult.Success)
                    return OperationResultModel<EmptyResult>.FailureResult(requirementsResult.Error);
                PatchRequirementsModel requirements = requirementsResult.Result!;

                Version currentVersion = new(ApplicationInfo.Version);
                Version patchVersion = new(requirements.version!);

                var versionResult = ValidatePatchVersion(currentVersion, patchVersion);
                if (!versionResult.Success)
                    return versionResult;

                var catalogResult = await ValidateMainCatalog(patchDirectory);
                if (!catalogResult.Success)
                    return catalogResult;

                var patchesResult = await DownloadRequiredPatches(
                    requirements,
                    currentVersion,
                    patchDirectory,
                    downloadWorker);
                if (!patchesResult.Success)
                    return OperationResultModel<EmptyResult>.FailureResult(patchesResult.Error);
                List<string> downloadedPatches = patchesResult.Result!;

                FinalizePatch(
                    downloadedPatches,
                    patchDirectory,
                    appItemDirectory);

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(HandleException(ex));
            }
            finally
            {
                try
                {
                    Directory.Delete(patchesDirectory, recursive: true);
                }
                catch (DirectoryNotFoundException)
                {
                    // The patch was not unpacked, so there is nothing to clean up.
                }
                catch (Exception ex)
                {
                    Basic.Logger.Instance.CreateWarningLog(nameof(ApplicationUpdateService), $"Cannot delete the patches folder {patchesDirectory}: {ex.Message}");
                }
            }
        }

        private async Task UnpackPatch(string archivePath, string patchDirectory) => 
            await ZipService.ExtractZip(archivePath, "/", patchDirectory);

        private OperationResultModel<PatchRequirementsModel> LoadPatchRequirements(string patchDirectory)
        {
            try
            {
                string requirementsFile = Path.Combine(patchDirectory, "requirements.json");
                PatchRequirementsModel requirements = JSONConvertor.LoadJson<PatchRequirementsModel>(requirementsFile);
                if (string.IsNullOrEmpty(requirements.version))
                    throw new UnknownFileFormatException();
                return OperationResultModel<PatchRequirementsModel>.SuccessResult(requirements);
            }
            catch (Exception ex)
            {
                return OperationResultModel<PatchRequirementsModel>.FailureResult(HandleException(ex));
            }
        }

        private OperationResultModel<EmptyResult> ValidatePatchVersion(Version current, Version patchVersion)
        {
            if (current >= patchVersion)
                return OperationResultModel<EmptyResult>.FailureResult(
                    HandleException(new NewestVersionAlreadyInstalledException()));
            return OperationResultModel<EmptyResult>.SuccessResult();
        }

        private async Task<OperationResultModel<EmptyResult>> ValidateMainCatalog(string patchDirectory)
        {
            try
            {
                string mainCatalogPath = Path.Combine(patchDirectory, "CDPIUI", "catalog.cat");

                await ValidateCatalog(Path.Combine(patchDirectory, "CDPIUI"));

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(HandleException(ex));
            }
        }

        private async Task<OperationResultModel<List<string>>> DownloadRequiredPatches(
            PatchRequirementsModel requirements,
            Version currentVersion,
            string patchDirectory,
            DownloadWorker? downloadWorker)
        {
            if (downloadWorker == null)
                return OperationResultModel<List<string>>
                    .FailureResult(HandleException(PrettyErrorCode.SERVER_UNREACHABLE));

            // The version of a required patch comes from its own requirements.json, not from the
            // shape of its URL (GitLab links were skipped), and the layers are applied by version,
            // oldest first, whatever the order of patch_urls. Every folder has its own index: a
            // name from the current second was shared by patches downloaded in the same second.
            var patches = new List<(Version Version, string Layer)>();
            try
            {
                int index = 0;
                foreach (string requirement in requirements.patch_urls ?? [])
                {
                    if (string.IsNullOrWhiteSpace(requirement))
                        continue;

                    string downloadDir = Path.Combine(patchDirectory, "required", (index++).ToString());

                    bool ok = await downloadWorker.DownloadAndExtractAsync(
                        requirement,
                        downloadDir,
                        extractArchive: true,
                        extractSkipFiletypes: [],
                        extractRootFolder: "/",
                        executableFileName: "",
                        filetype: Shared.Basic.Filesystem.FileExtensionTypes.archive);

                    if (!ok)
                        return OperationResultModel<List<string>>.UnSuccessResult();

                    var requiredResult = LoadPatchRequirements(downloadDir);
                    if (!requiredResult.Success || !Version.TryParse(requiredResult.Result!.version, out Version? requiredVersion))
                        throw new UnknownFileFormatException();

                    Logger.Instance.CreateInfoLog(nameof(ApplicationUpdateService), $"Required patch {requiredVersion}");
                    if (currentVersion >= requiredVersion)
                        continue;
                    // The main patch is always the last layer; a required patch that is not older
                    // than it would be applied first and then partly overwritten.
                    if (Version.TryParse(requirements.version, out Version? mainVersion) && requiredVersion >= mainVersion)
                        continue;

                    string layer = Path.Combine(downloadDir, "CDPIUI");
                    await ValidateCatalog(layer);
                    patches.Add((requiredVersion, layer));
                }
            }
            catch (Exception ex)
            {
                return OperationResultModel<List<string>>.FailureResult(HandleException(ex));
            }

            return OperationResultModel<List<string>>.SuccessResult(
                patches.OrderBy(patch => patch.Version).Select(patch => patch.Layer).ToList());
        }

        private static async Task ValidateCatalog(string dir)
        {
            string catalogPath = Path.Combine(dir, "catalog.cat");
            if (!File.Exists(catalogPath))
                throw new FileNotFoundException("Catalog file not found");

            var catalogCheck = await CertificateCheck.CheckCatalog(catalogPath, dir);
            if (catalogCheck != CatalogCheckResult.Success)
                throw new CatalogInvalid();
        }

        /// <summary>
        /// Packs the required patches, oldest first, and the main patch as separate layers
        /// (layers/0/, layers/1/, ...). Each layer keeps its own signed catalog, so the elevated
        /// tray can check every signature again before Update.exe applies the layers in order.
        /// </summary>
        private void FinalizePatch(List<string> patches, string patchDirectory, string appItemDirectory)
        {
            var layers = new List<string>(patches) { Path.Combine(patchDirectory, "CDPIUI") };

            string finalPatchFile = Path.Combine(appItemDirectory, "patch.cdpipatch");
            if (File.Exists(finalPatchFile))
                File.Delete(finalPatchFile);

            using ZipArchive archive = ZipFile.Open(finalPatchFile, ZipArchiveMode.Create);
            for (int i = 0; i < layers.Count; i++)
            {
                foreach (string file in Directory.EnumerateFiles(layers[i], "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(layers[i], file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, $"layers/{i}/{relativePath}");
                }
            }
        }

        private static ErrorModel HandleException(Exception ex)
        {
            var model = ErrorsHelper.Convertor.GetPrettyErrorCode("ERR_APP_UPDATE", ex);
            return new() { ErrorCode = model };
        }
        private static ErrorModel HandleException(PrettyErrorCode errorCode)
        {
            return ErrorModel.OnlyErrorCode($"ERR_STORE_{errorCode}");
        }
    }
    
}
