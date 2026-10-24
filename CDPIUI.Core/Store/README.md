**English** | [Русский](README.ru.md)

# Store catalogue and item checks

This document describes the Store repository that the GUI reads, the JSON fields of the catalogue and the checks for each download type.
The install sequence, the queue and the rollback are in the Store section of [CDPIUI.Core/README.md](../README.md#store).
The install actions (`before_install_actions`, `after_install_actions`) are in [LScript/README.md](../LScript/README.md#install-actions).

The code is in these files:

| Part | Files |
|---|---|
| Repository and catalogue | `Repository/RepositoryLoaderService.cs`, `Repository/*InitializationModel.cs`, `Repository/VersionControlData.cs`, `ViewModels/RepoItemModel.cs`, `ViewModels/ReadyKitModel.cs` |
| Download links | `OnlineItemsInstallationService.cs`, `Network/APIWorker.cs`, `Network/Models/*.cs` |
| Download and extraction | `Network/RequestWorker.cs`, `Network/DownloadWorker.cs`, `CDPIUI.Core/System/ZipService.cs`, `CDPIUI.Shared/Basic/Filesystem/ArchivePaths.cs` |
| File types | `FileExtensionTypes` and `FileSystemService` in `CDPIUI.Shared/Basic/Filesystem/FileSystemService.cs` |
| Catalog signature | `CDPIUI.Core/Security/CertificateCheck.cs`, `CDPIUI.Shared/Security/CatalogVerifier.cs` |
| Local packs | `LocalItemsInstallerHelper.cs`, `StoreHelper.cs` (`GetReadyLocalItem`, `CopyLocalPack`) |
| Tray checks | `CDPIUI.TrayIcon/Helper/MsiInstallerHelper.cs`, `PackageStaging.cs`, `ExecutableTrust.cs`, `TrustedPaths.cs` |

`JSONConvertor` reads all catalogue files with Newtonsoft.Json and its default settings. Fields that the client does not know are ignored.

## Repository source

The setting `STORE/versionControlType` selects the repository host. The default value is `GitHub`.

| Host | Repository (`SharedConstants`) | Archive URL (`VersionControlData.VersionControlsLinks`) |
|---|---|---|
| `GitHub` | `StoreRepo` = `Storik4pro/CDPIUI-Store` | `https://api.github.com/repos/Storik4pro/CDPIUI-Store/zipball/main` |
| `GitLab` | `GitLabStoreRepo` = `Storik4/CDPIUI-Store` | `https://gitlab.com/Storik4/CDPIUI-Store/-/archive/main/CDPIUI-Store-main.zip` |

The client reads the branch `main` only. It does not use releases or tags of the Store repository.

`LoadAllStoreDatabase` downloads the archive in these cases:

- The folder `Store\Cache\Repo` does not exist.
- A sync is forced and the last sync (`STORE/lastSyncTime`) is one day old or more.

If the GitHub download fails, the client downloads the GitLab archive.

The repository archive gets these checks:

- The URL must be an absolute HTTPS URL (`RequestWorker.CreateRequest`).
- The archive must be within the limits of `ArchivePaths.EnsureWithinLimits` (see [Archive limits](#archive-limits)).
- Each entry path must stay in the target folder, and the path must not go through a link (`ArchivePaths.GetDestinationOrThrow`, `EnsureNoReparsePoints`).

The repository has no signature. The client does not check a catalog for it.

The client removes the first path segment of each entry. This segment is the root folder of the zipball. An entry with only one segment is skipped.
The client extracts the archive into `Repo.new-<GUID>`. It replaces `Store\Cache\Repo` only after a full extraction.

## Repository layout

```text
<repository root>/
├── init.json                         # Repository file (RepositoryInitializationModel)
├── <file from localized_strings_directory>   # One localization file for each language
├── <folder from categories_directory>/
│   ├── init.json                     # Category file (RepoCategoryInitializationModel)
│   └── <folder from items_directories>/
│       └── init.json                 # Catalogue item (RepoItemModel)
└── <kits_directory>/
    ├── init.json                     # Kit list (ReadyKitsInitializationModel)
    └── <folder from kits_directories>/
        └── init.json                 # Ready kit (ReadyKitModel)
```

All paths in these files are relative. A category folder is relative to the repository root. An item folder is relative to its category folder. A kit folder is relative to `kits_directory`.
The client reads only the files in this tree. It ignores other files in the repository.

### Repository file (`init.json`)

| Field | Type | Required | Meaning |
|---|---|---|---|
| `categories` | List of strings | Yes | Category names in display sequence. The name is also the localization key of the category title. |
| `categories_directory` | Object: name → folder | Yes | The folder of each category. A name without an entry is skipped. |
| `localized_strings_directory` | Object: language code → file | No | The localization file of each language, for example `EN` and `RU`. `EN` is the fallback. Without this field, all names show as `slocale:<name>`. |
| `kits_directory` | String | No | The folder of the ready kits. |

If `categories` is set and `categories_directory` is missing, the client cannot load the catalogue. It reports the error `Cannot load Items: <message>` (`StoreInternalErrorHappens`).

### Localization file

A localization file is a JSON object. Each key is a name from the catalogue. Each value is the translated text.
`GetLocalizedStoreItemName` uses the file of the GUI language. If the language has no file, it uses the `EN` file.

- A name that contains a space is shown without change.
- A name without a translation is shown as `slocale:<name>`.

### Category file (`<category>/init.json`)

| Field | Type | Required | Meaning |
|---|---|---|---|
| `store_id` | String | Yes | The category id. Each item of the category gets it as `category_id`. |
| `type` | String | Yes | `basic_category` or `second_category`. The client skips a category with a different value. |
| `items` | List of strings | Yes | Item names in display sequence. |
| `items_directories` | Object: name → folder | Yes | The folder of each item. A name without an entry is skipped. |

A damaged category file skips only this category. A damaged item file skips only this item.

### Catalogue item (`<category>/<item>/init.json`)

The field names must have this spelling, also `preffered_to_download_file_name`.

| Field | Type | Required | Meaning |
|---|---|---|---|
| `store_id` | String | Yes | The item id. It must agree with `^[A-Za-z0-9_-]{1,64}$` (`StoreItemIds.IsValid`), else the installation fails. It is also the name of the item folder. |
| `type` | String | Yes (not checked) | The item type. The code uses `component`, `configlist` and `addon`. The value goes to the `Type` column of the item database. |
| `name` | String | Yes (not checked) | The display name. The client translates it with the localization file. |
| `short_name` | String | No | The short display name. |
| `developer` | String | No | The developer name. |
| `icon` | String | No | The icon. The GUI evaluates it with LScript, for example `$DYNAMICIMAGE(...)`. |
| `background` | String | No | The background color as a hex value. |
| `stars` | String | No | The rating text. The item page shows `NaN` when it is missing. |
| `small_description`, `description` | String | No | The descriptions. The GUI evaluates them with LScript for the Store language (`EN` or `RU`). |
| `display_warning` | Boolean | No | `true` shows `warning_text` on the item page. |
| `warning_text` | String | No | The warning text. |
| `links` | List of `{ "name", "url" }` | No | Links on the item page. |
| `license` | List of `{ "name", "url" }` | No | Licenses. The GUI shows them before the installation. |
| `dependencies` | List of `[ itemId, version ]` | No | The items that this item needs. An entry with fewer than two values is ignored. |
| `target_minversion`, `target_maxversion` | String | No | The range of application versions that support the item (`VersionHelper.IsVersionInRange`). |
| `version_control` | String | Yes | How the client finds the download. See [Download sources](#download-sources). |
| `version_control_link` | String | For `git` and `git_only_last` | The repository URL on GitHub or GitLab. The client reads the releases of this repository. It also uses this URL for the update check. |
| `download_link` | String | For `git_only_last` and `subscription` | The direct HTTPS URL of the file. |
| `filetype` | String | For `git`, `git_only_last` and `subscription` | The download type. See [Download types](#download-types). |
| `preffered_to_download_file_name` | String | No | Text that the release asset name must contain (case-sensitive). |
| `sha256` | String | No | SHA-256 of the downloaded file (64 hex digits). See [SHA-256 of a download](#sha-256-of-a-download). |
| `archive_root_folder` | String | No | The folder in the archive to extract. See [Extraction root](#extraction-root). |
| `target_executable_file` | String | No | The file name without extension for a file that is not extracted. For a component, the GUI starts `<item folder>\<target_executable_file>.exe`. It must be a file name only: a value with `\`, `/` or `..` stops the download. |
| `files_to_download` | List of download entries | For `several_repos` | See [Download entry](#download-entry-files_to_download). |
| `before_install_actions` | String | No | LScript actions before the database registration. |
| `after_install_actions` | String | No | LScript actions after the database registration. |

The client sets `category_id` itself. Do not write it in the file.

### Download entry (`files_to_download[]`)

`several_repos` items have one entry for each file. The client downloads the entries in the list sequence into the same item folder.

| Field | Type | Required | Meaning |
|---|---|---|---|
| `type` | String | Yes | The download type. See [Download types](#download-types). |
| `version_control` | String | No | `external_site_only_last` uses `download_link`. All other values, and a missing value, use the release API. |
| `version_control_link` | String | For the release API | The repository URL on GitHub or GitLab. |
| `download_link` | String | For `external_site_only_last` | The direct HTTPS URL of the file. |
| `preffered_version` | String | No | The release tag to download. Empty means the latest release. |
| `preffered_to_download_file_name` | String | No | Text that the release asset name must contain (case-sensitive). |
| `sha256` | String | No | SHA-256 of the downloaded file (64 hex digits). See [SHA-256 of a download](#sha-256-of-a-download). |
| `archive_root_folder` | String | No | The folder in the archive to extract. |
| `actions` | String | No | `remove` deletes the copied file after the download and after an MSI installation. All other values keep the file. |

A download entry has no `target_executable_file`. The client takes the file name from the URL.
The version of a `several_repos` item is the version of the last entry that has a version. An `external_site_only_last` entry has no version.

### Ready kits

The kit list (`<kits_directory>/init.json`) has these fields:

| Field | Type | Required | Meaning |
|---|---|---|---|
| `kits` | List of strings | Yes | Kit names in display sequence. |
| `kits_directories` | Object: name → folder | Yes | The folder of each kit. A name without an entry is skipped. |

A kit file (`<kit>/init.json`) has these fields:

| Field | Type | Required | Meaning |
|---|---|---|---|
| `store_id` | String | Yes | The kit id. |
| `items` | List of strings | Yes | The ids of the items in the kit. The list must not be empty. |
| `name`, `short_name`, `icon`, `background`, `small_description`, `description` | String | No | Display data, as for an item. |
| `recommended` | Boolean | No | Marks a recommended kit. The old spelling `recomended` is also read. |

A kit without `store_id` or with an empty `items` list is skipped.

CAUTION: A kit file that is not valid JSON stops the load of all kits that come after it in `kits`.

## Download sources

`version_control` selects how the client finds the download link. The value is not case-sensitive. An unknown value causes an exception, and the installation fails.

| `version_control` | Link | Version |
|---|---|---|
| `git` | The release asset of `version_control_link` that agrees with `filetype` | The release tag |
| `git_only_last` | `download_link`. The client also reads the release of `version_control_link`, and an asset must agree with `filetype`. | The release tag |
| `several_repos` | One link for each entry of `files_to_download` | The version of the last entry that has one |
| `subscription` | `download_link` | Always `0.0.0` |

The installed application item (`CDPIUIAppSt`) is not in the catalogue. `GetItemInfoFromStoreId` makes its data: `version_control` is `git`, `target_executable_file` is `patch`, and `filetype` is `CDPIUIUpdateItem` (portable) or `UPDmsi` (MSI installation).

### Release API

`APIWorker` reads the release from the host of the Store setting (`STORE/versionControlType`), not from the host of the URL:

| Host | Latest release | Given version |
|---|---|---|
| GitHub | `https://api.github.com/repos/<owner>/<repo>/releases/latest` | `.../releases/tags/<version>` |
| GitLab | `https://gitlab.com/api/v4/projects/<owner%2Frepo>/releases/permalink/latest` | `.../releases/<version>` |

The client selects the release asset with these rules:

1. The asset name is equal to the `filetype` value, or the name ends with the extension of the type (see [Download types](#download-types)).
2. If `preffered_to_download_file_name` is set, the name contains it.
3. Exactly one asset must agree. No asset gives `INVALID_URI`. More than one asset gives `TOO_MANY_VARIANTS`.

The release API needs a token (`Secret.GitHubToken` or `Secret.GitLabToken`). If the token is empty, the request fails. A build with the secret stub cannot install items from the release API.

### Update check

`StoreHelper.CheckUpdates` checks all installed items except `LocalUserStorage` and items with `version_control` `local`.
It uses the stored update URL when its host agrees with the Store host. Else it uses `version_control_link` from the catalogue.
An update is available when the latest release tag is newer than the installed version (`VersionHelper.CompareVersionStrings`).

## Checks for all online downloads

`RequestWorker` and `DownloadWorker` apply these checks to each download:

- The URL must be an absolute HTTPS URL. This also applies to the `DOWNLOAD=` requests of the install actions. A `DOWNLOAD=` request can also have a SHA-256: see `download_easy_designer_annotation_file` in [LScript/README.md](../LScript/README.md).
- The client sends the Store token only to `github.com`, `api.github.com`, `codeload.github.com`, `raw.githubusercontent.com`, `objects.githubusercontent.com` and `gitlab.com`.
- If the server gives the file size, the drive must have enough free space.
- A download stops when no data arrives for 60 seconds.
- An HTTP error status gives `ERR_DOWNLOAD_UNEXPECTED_STATUS_CODE_<code>`.
- The file goes to `TempFiles\Downloads` first. The client deletes it after the operation.

When `STORE/ProxyEnable` is `true` and the Store host is GitHub, the client sends the requests through the Store proxy (`Secret.ProxyURL`).

### SHA-256 of a download

`DownloadWorker.DownloadAndExtractAsync` has the parameter `expectedSha256`. When it is set, the client calculates the SHA-256 of the downloaded file before the extraction or the installation.

- Characters that are not hex digits are ignored. The value must have 64 hex digits.
- If the hash is different, or the value does not have 64 hex digits, the download fails with `ERR_DOWNLOAD_HASH_MISMATCH`.

A catalogue file can set the hash in the field `sha256`:

- In an entry of `files_to_download`, `APIWorker.GetDownloadLinksAsync` copies `FileToDownload.sha256` to `DownloadLinkModel.sha256`.
- In an item without `files_to_download`, `OnlineItemsInstallationService` copies `RepoItemModel.sha256` to the link from the release API.

`OnlineItemsInstallationService` sends `DownloadLinkModel.sha256` to `expectedSha256`. An empty or missing field turns the check off.

CAUTION: A hash pins one file. Set it only together with a fixed version (`preffered_version`) or a fixed link. With `git_only_last` or `subscription`, each new release has a new hash, and the client rejects the download until the catalogue is updated.

## Download types

The type is a value of `FileExtensionTypes`. `OnlineItemsInstallationService` reads it case-insensitively. An unknown value becomes `temp`.
The release API needs a known type: for an unknown value, the asset selection causes an exception, and the installation fails.
The extension column gives the extension that the release asset must have and the extension of a copied file (`FileSystemService.FileExtensions`).

| Type | Extension | Processing | Checks |
|---|---|---|---|
| `signedZip` | `.cdpisignedpack` | Extracted into `<item folder>.verify-<GUID>`. The files move into the item folder only when the catalog is valid. Existing files are replaced. | Archive limits, catalog signature |
| `archive` | `.zip` | Extracted directly into the item folder. `.txt` files are merged. | Archive limits |
| `configPack` | `.cdpiconfigpack` | The same as `archive` | Archive limits |
| `WIN32application` | `.exe` | Copied into the item folder | None in the GUI. The tray checks the program when it starts it (see [Elevated start](#elevated-start)). |
| `msi` | `.msi` | Copied into the item folder. Then the tray installs it with `msiexec.exe /i "<file>" /passive /norestart`. | Authenticode in the tray |
| `elmsi` | `.exe` | Copied into the item folder. Then the tray starts it with `/install /passive /norestart`. | Authenticode in the tray |
| `CDPIUIUpdateItem` | `.cdpipatch` | Application item only. Copied as `patch.cdpipatch`. Then `ApplicationUpdateService` prepares the patch. | Catalog signature of each layer, in the GUI and in the tray |
| `UPDmsi` | `.msi` | Application item only. Copied as `patch.msi`. The GUI does not install it. The tray starts it for the application update. | Pinned Authenticode signature in the tray |
| `lst` | `.lst` | Copied into the item folder | None |
| `raw` | `.txt` | Copied into the item folder | None |
| `temp`, `none` | `.cdpitempfile`, none | Copied into the item folder | None |

Only `signedZip`, `archive` and `configPack` are extracted (`FileSystemService.CompressedFileTypes`).
Online downloads never skip files during the extraction. Only local packs that are not signed have a skip list.

### Name of a copied file

A file that is not extracted gets the name `<name><extension>`:

- `<name>` is `target_executable_file` when it is set. A download entry of `several_repos` has no `target_executable_file`.
- Else `<name>` is the last segment of the URL path without its extension.
- `<extension>` comes from the table above.

For `msi` and `elmsi`, the GUI always sends `<item folder>\<URL file name without extension><extension>` to the tray.

NOTE: For an `msi` or `elmsi` item, the installer is started from the copied file, so `target_executable_file` also sets the name of the installer.

If an `msi` or `elmsi` installation fails, the client deletes the copied installer.

### Catalog signature

The catalog check (`ZipService.VerifyCatalog`, `CatalogVerifier.VerifyAsync`) has these steps:

1. The file `catalog.cat` must be in the extraction root. If it is missing, the result is `NoSignature`.
2. Each signer certificate of the catalog must be in `Secret.TrustedCertificates` (SHA-1 thumbprints). The signature must be valid. The chain is not built.
3. `Test-FileCatalog` (Windows PowerShell) must give `Valid` for the extracted folder. Only `catalog.cat` is skipped. The time limit is 2 minutes.

If one step fails, the extraction folder is deleted and the installation fails. No file reaches the item folder.
`catalog.cat` moves into the item folder with the other files. `ExecutableTrust` uses it later.

### Archive limits

`ArchivePaths` applies these limits to Store archives, local packs and application patches:

| Limit | Value |
|---|---|
| Entries | 20000 at most (`MaxEntries`) |
| Unpacked size | 1 GiB at most (`MaxTotalBytes`) |
| Compression ratio | 200 at most for an entry of 1 MiB or more (`MaxCompressionRatio`, `RatioCheckMinimumBytes`) |
| Entry size | An entry must not give more data than its header says (`ExtractToFileBounded`) |
| Path | The path must stay in the target folder. A drive letter or a rooted path is rejected. |
| Links | The target path and its folders must not be links or junctions (`EnsureNoReparsePoints`). |

### Extraction root

`archive_root_folder` selects the folder of the archive that the client extracts (`ZipService.ExtractEntries`):

- An empty value or `/` extracts the full archive.
- The client removes a `/` at the end and divides the value into segments at `/`.
- Each segment must be equal to the segment of the entry path. The comparison is not case-sensitive.
- `$ANY` agrees with any one segment, for example the versioned root folder of a release archive.
- Entries outside the root are ignored. The client removes the root segments from the path of each extracted entry.

Example: with `archive_root_folder` `$ANY/bin`, the entry `<any folder>/bin/<file>` is extracted as `<file>`, and the entry `<any folder>/<file>` is ignored.

### Merged text files

When the archive is not signed, the client merges `.txt` files:

- If the target `.txt` file exists, the client adds the lines of the archive that are not empty and not yet in the file. It compares trimmed lines.
- If `<file>.txt.bak` exists, the client adds the lines to the `.bak` file and not to the `.txt` file.

A signed archive replaces `.txt` files.

## Local packs

The GUI accepts local packs from a file activation (`App.ProcessFiles`) and from the Store settings (`ManualItemInstallation_Click` in `Views/Store/SettingsPage.xaml.cs`). Both open `StoreLocalItemInstallingDialog`.

| File | Signed | Catalog check | Skipped files | `ExecutableFile` and install actions |
|---|---|---|---|---|
| `.cdpisignedpack` | Yes | Yes | None | Used |
| `.cdpiconfigpack` and all other extensions | No | No | See below | Ignored |

`LocalItemsInstallerHelper` compares the extension with `.cdpisignedpack` case-sensitively. A file with the extension `.CDPISIGNEDPACK` is processed as a pack that is not signed.

A pack that is not signed cannot bring files with these extensions (`UnsignedPackSkippedFiles`):
`.exe`, `.dll`, `.sys`, `.msi`, `.com`, `.scr`, `.cpl`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.vbe`, `.js`, `.jse`, `.wsf`, `.hta`, `.lnk`, `.url`.
The client skips these files during the extraction. It does not show an error.

The client processes a local pack in this sequence:

1. It extracts the full archive into `TempFiles\Offline\<temporary name>`. A signed pack goes through a `.verify-<GUID>` folder and the catalog check.
2. The file `init.json` must be in the archive root. Else the error is `ERR_LOCAL_ITEM_UNSUPPORTED`.
3. `StoreId` must agree with `^[A-Za-z0-9_-]{1,64}$`. It must not be `CDPIUIAppSt` or `LocalUserStorage`.
4. The queue moves the extracted folder to `Store\Items\<StoreId>`. The old folder moves to `<folder>.previous-<GUID>` and comes back if the move fails.
5. The client registers the item with `filetype` `localPack` and `version_control` `local`. The update check skips it.

The archive limits apply to all local packs. A pack icon (`$DYNAMICIMAGE(...)`) is shown in the dialog only when the file is in the pack and has the extension `.png`, `.jpg`, `.jpeg`, `.ico`, `.bmp` or `.gif`.

### Pack file (`init.json` of a local pack)

The fields come from `LocalItemInitModel`.

| Field | Type | Required | Meaning |
|---|---|---|---|
| `StoreId` | String | Yes | The item id and the folder name. |
| `Type` | String | No | The item type. The default is `configlist`. |
| `Version` | String | No | The installed version. |
| `Name` | String | No | The display name. The default is `ShortName`, then `StoreId`. |
| `ShortName` | String | No | The short name. The default is `StoreId`. |
| `Icon` | String | No | The icon, for example `$DYNAMICIMAGE(icon.png)`. The client stores the full path when the file is in the item folder. |
| `Color` | String | No | The background color. |
| `Developer` | String | No | The developer name. |
| `Requirements` | List of `[ itemId, version ]` | No | The same as `dependencies` of a catalogue item. |
| `ExecutableFile` | String | No | The same as `target_executable_file`. Signed packs only. |
| `BeforeInstallActions`, `AfterInstallActions` | String | No | The same as `before_install_actions` and `after_install_actions`. Signed packs only. |

### Application patch (`.cdpipatch`)

A local `.cdpipatch` file opens `ApplicationUpdateFileDialogWindow`. `ApplicationUpdate.InstallApplicationUpdateFromFile` adds the item `CDPIUIAppSt` to the queue with the file.
The client copies the file to `Store\Items\CDPIUIAppSt\patch.cdpipatch`. In a portable installation, `ApplicationUpdateService` then checks the patch. The steps and the format are in [.github/RELEASES.md](../../.github/RELEASES.md#how-the-application-applies-a-patch).

### Shared preset (`.cdpiconfig`)

A `.cdpiconfig` file is not a Store item. ConfigShare (`CDPIUI.AddOns/ConfigShare`) reads it. ConfigShare has its own limits: 4096 entries, 512 MiB, a compression ratio of 200 for entries above 1 MiB, a manifest of 4 MiB at most and a SHA-256 for each resource. It rejects symbolic links and options that write files outside the package. It does not run install actions.
Refer to [CDPIUI.AddOns/ConfigShare/README.md](../../CDPIUI.AddOns/ConfigShare/README.md).

## Summary of the checks

| Source | Catalog signature | Authenticode | Archive limits | Skipped files | Install actions | `target_executable_file` |
|---|---|---|---|---|---|---|
| Online `signedZip` | Yes | No | Yes | None | Yes | Yes |
| Online `archive`, `configPack` | No | No | Yes | None | Yes | Yes |
| Online `WIN32application`, `lst`, `raw` | No | No | Not applicable | Not applicable | Yes | Yes |
| Online `msi`, `elmsi` | No | Yes, in the tray (chain to a trusted root) | Not applicable | Not applicable | Yes | Do not set |
| `.cdpisignedpack` | Yes | No | Yes | None | Yes | Yes (`ExecutableFile`) |
| `.cdpiconfigpack` and other local packs | No | No | Yes | `UnsignedPackSkippedFiles` | Ignored | Ignored |
| `.cdpipatch` (online or local) | Yes, each layer, in the GUI and in the tray | No | Yes | None | No | Not applicable |
| `patch.msi` (`UPDmsi`) | No | Yes, pinned certificate, in the tray | Not applicable | Not applicable | No | Not applicable |
| `install_msi` action | No | Yes, in the tray | Not applicable | Not applicable | Not applicable | Not applicable |

## Installation folder

All Store items go to `<DataDirectory>\Store\Items\<store_id>`. `DataDirectory` is described in [CDPIUI.Core/README.md](../README.md#data-directories).

- No caller sets `cleanDirectoryBeforeInstalling` to `true`. Thus an online update writes over the old folder, and files of earlier versions stay.
- The item database (`storedata.db`) keeps the folder, `target_executable_file` as `Executable`, `filetype` as `DownloadFileType`, `version_control` as `VersionControlType` and `version_control_link` as `UpdateCheckUrl`.

## Installers in the tray

`MsiInstallerService` sends the installer path to the tray. `MsiInstallerHelper.ProcessMsi` in the tray does these steps:

1. The path must be in an application folder (`TrustedPaths.IsAllowed`). The allowed folders are the tray folder, `%LOCALAPPDATA%\CDPIUI` of the tray user and `%LOCALAPPDATA%\CDPIUI` of the desktop user.
2. `PackageStaging.Stage` copies the file to `<tray folder>\.staging\<GUID>\`. The tray checks and runs this copy.
3. `PackageStaging.HasValidSignature` calls `WinVerifyTrust`. The signature must be valid and chain to a trusted root. The revocation is not checked.
4. A `.msi` file starts with `msiexec.exe /i "<file>" /passive /norestart`. Other files start with `/install /passive /norestart`.
5. The exit codes `0` and `1638` give `Complete`. `3010` and `1641` give `CompleteRestartRequest`. Other codes give `ExceptionHappens`.

The GUI waits 30 minutes at most for the result.

## Elevated start

The tray checks each program before it starts it with administrator rights: a component process, the help output capture and GoodCheck (`ExecutableTrust.AcquireAsync`).

1. The path must be in an application folder (`TrustedPaths.IsAllowed`). Else the program does not start.
2. If the installation is portable (`Utils.IsInstalledByMsi` is `false`) or the program is in the tray folder, there is no other check.
3. The tray opens read handles on the program and on all `.exe`, `.dll` and `.sys` files in its folder (`ExecutableLease`). These files cannot change while the handles are open.
4. The tray calculates a fingerprint: SHA-256 over the names and the SHA-256 of these files.
5. If `<tray folder>\.trust\approved.txt` has the same fingerprint for the path, the program starts.
6. Else, if the program is in a Store item folder (`Store\Items\<id>`) with a valid `catalog.cat`, the program starts. This check compares only the code files (`.exe`, `.dll`, `.sys`). All other file names are skipped, because the user can change lists and settings.
7. Else the tray asks the user in a message box. The box shows the path, the SHA-256 and the signer. The default answer is "No".
8. After a "Yes" or a valid catalog, the tray writes the fingerprint to `approved.txt`.

Thus, in an MSI installation, a `signedZip` item or a `.cdpisignedpack` with a catalog starts without a question. A component from an `archive` or a `WIN32application` download needs the approval of the user after each change of its code files.
