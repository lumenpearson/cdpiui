**English** | [Русский](README.ru.md)

# CDPIUI.Core

`CDPIUI.Core` is a .NET 8 class library. It contains the logic of the GUI process
that does not use WinUI: components, presets, the Store, the pipe client and settings.
The library does not start component processes. The elevated tray process
(`CDPIUI.TrayIcon`) starts them when the library sends a request through the pipe.

## Projects that use this library

| Project | Reference |
|---|---|
| `CDPIUI` (GUI) | `CDPIUI/CDPIUI.csproj` |
| `CDPIUI.AddOns` | `CDPIUI.AddOns/CDPIUI.AddOns.csproj` |
| `CDPIUI.Tests` | `CDPIUI.Tests/CDPIUI.Tests.csproj`; internal types are visible to it (`InternalsVisibleTo`) |

`CDPIUI.TrayIcon` does not use `CDPIUI.Core`. Both processes use `CDPIUI.Shared`.
The library uses the packages `Microsoft.Data.Sqlite`, `Newtonsoft.Json` and `Semver`.
It also pins `SQLitePCLRaw.lib.e_sqlite3` to version 2.1.14. The version that `Microsoft.Data.Sqlite` brings has a known SQLite vulnerability.

## General rules

- Most services are lazy singletons with a static `Instance` property and a lock.
  Examples are `SettingsManager`, `StoreHelper`, `DatabaseHelper` and `ComponentTasksManager`.
- The namespace `CDPIUI.Core.System` hides the namespace `System` in code inside `CDPIUI.Core`.
  Use `global::System...` when the short name is not clear, for example
  `global::System.IO.Directory.Exists` in `ComponentServices/Helpers/ComponentHelper.cs`.
- `ConfigurationService` is in the namespace `CDPIUI.Core.ComponentServices.Configuration`.
  Its file is in the folder `ComponentServices/Helpers/Configuration`.
- Operations that can fail usually return `OperationResultModel<T>` (from `CDPIUI.Shared`).
  Do not throw exceptions to the UI when a result model is available.

## Root files

| File | Responsibility |
|---|---|
| `Application.cs` | `ApplicationInfo`: the UI language (`Localization`, default `en-US`) and `Version`. `Version` is the file version of the entry assembly, read one time. |
| `CoreEvents.cs` | `CoreEvents`: the event `CriticalCoreExceptionHappens` (the last error is in `LastCriticalError`) and the event `RestartRequired`. `IsRestartPending` stays `true` until the UI calls `AcknowledgeRestart`. |
| `SettingsManager.cs` | `SettingsManager`: XML settings in `Settings/Settings.xml`. A missing key gets its default value from `Template/Settings/Settings.xml` in the application folder. |
| `State.cs` | `State`: the installation kind, `IsApplicationBuildAsSingleFile` (portable) and `IsApplicationBuildAsMsi`. See "Installation kind". |

Settings use a group and a key, for example `GetValue<string>("PROXY", "proxyType")`.
Component settings use a group path, for example `["CONFIGS", componentId]`.

### Installation kind

`State` gives the installation kind:

- The `SingleFileRelease` configuration defines `SINGLEFILE`. The installation is then always portable.
- The `MsiRelease` configuration defines `MSIFILE`. The installation is then always an MSI installation.
- Without these symbols (for example `Release` or `Debug`), `State` finds the kind at run time, one time.
  The MSI writes its installation folder to the registry value `HKLM\Software\Storik4\CDPI UI\InstallLocation` (64-bit view, component `InstallLocationRegistration` in `NewSetup/Product.wxs`).
  If this value is equal to the application folder (not case-sensitive), the installation is an MSI installation. Otherwise it is portable.

The release workflow packs the same `Release` build as the MSI and as the portable ZIP. Thus the run-time check is necessary.
The tray uses the same registry check (`CDPIUI.TrayIcon/Helper/Utils.cs`).
The installation kind selects the data directory, the update file (`patch.cdpipatch` or `patch.msi`) and the Store file type of the application update.

## Data directories

`Data/Directories.cs` gives all paths. Use these properties. Do not make paths by hand.

| Property | Path |
|---|---|
| `CurrentDirectory` | The folder of the application executable. |
| `DataDirectory` | `CurrentDirectory` when the process can write to it and the installation is not an MSI installation (`State.IsApplicationBuildAsMsi`). Otherwise `%LOCALAPPDATA%\CDPIUI`. |
| `SettingsFilePath` | `<DataDirectory>\Settings\Settings.xml` |
| `TemplateSettingsFilePath` | `<CurrentDirectory>\Template\Settings\Settings.xml` |
| `StoreItemsDirectory` | `<DataDirectory>\Store\Items`. Each installed item has the folder `Items\<itemId>`. |
| `StoreLocalUserItemDirectory` | `Store\Items\LocalUserStorage` (user presets, with the subfolders `List`, `Bin` and `Loc`). |
| `StoreLocalCacheDirectory` | `Store\Cache\Local`. It contains the item database `storedata.db`. |
| `StoreRepoCacheDirectory` | `Store\Cache\Repo`. It contains the extracted Store repository. |
| `TempFilesDirectory` | `<DataDirectory>\TempFiles` |
| `DownloadManagerDirectory` | `TempFiles\Downloads` |
| `ELUADirectory` | `<CurrentDirectory>\ELUA` |

If `DataDirectory` cannot be found, the library uses `%LOCALAPPDATA%\CDPIUI` and raises
`CriticalCoreExceptionHappens`.

## Basic

- `Logger`: the log singleton (`LoggerBase` from `CDPIUI.Shared`) in `DataDirectory`.
  Debug builds write all levels. Other builds use the setting `DEBUG/logLevel` (default `WARN`).
- `ErrorsHelper.Convertor` (`CustomErrorConvertor`): converts exceptions to error codes and `ErrorModel`.
  An `HttpRequestException` with a status code gives `UNEXPECTED_STATUS_CODE`.
- `IniDocument`: reads and changes an INI file (GoodCheck `config.ini`). It keeps comments, blank lines and the order of keys, and changes only the lines of the values that are set.

## Communication

The GUI is a pipe client. The tray process is the pipe server.

- `PipeClientService`: connects to the pipe that the session file `.session\pipe.key` in the application folder names. The tray makes a new pipe name at each start.
  - It waits 2 seconds. If the tray process runs, it waits 15 more seconds. During the wait, it reads the session file again each 0.5 seconds.
  - If there is no connection, it starts `CDPIUI_ElevatedLauncher.exe tray <arguments>` as administrator (`runas`) and stops the GUI process. The launcher removes the .NET variables of the user profile and starts `CDPIUI_TrayIcon.exe` (see [CDPIUI.ElevatedLauncher/README.md](../CDPIUI.ElevatedLauncher/README.md)).
    The tray gets `--autorun` when `APPEARANCE/hideToTrayOnStartup` is `true`. Otherwise it gets `--show-ui`.
    When the GUI has activation arguments or a GoodbyeDPI migration argument, the tray gets `--show-ui` and these arguments.
  - It closes the connection and stops the GUI when the server is not the expected `CDPIUI_TrayIcon.exe` (`PipePeerVerifier`).
  - It reads the pipe name and the pipe key together for each connection attempt and sends the key in the auth packet (see [Pipe](../CDPIUI.Shared/Pipe/README.md#session-file)).
  - A change in a settings group `CONFIGS/<componentId>` sends `ProcessIdStartupArgsChanged` and the setup state of the component.
- `PipeHelper`: static methods that send typed packets (for example settings, update, notification, ConPTY, MSI, proxy, conditional launch).
- `CoreCommandsHandler`: handles the packets from the tray (service, ConPTY, settings, MSI, application, conditional launch).
  Conditional launch actions send `SendConditionalLaunchResult` with success or the error text.
- `ConPtyHelpCaptureClient`: asks the tray to run an executable and returns its help output (`CaptureHelpAsync`).

## ComponentServices

### Main types

- `ComponentTasksManager`: a `TasksManageService<ProcessService>` with one task for each installed component.
  `UpdateTaskList` stops all tasks, makes the list again from the database, and starts again the tasks that ran before.
- `ProcessService`: the state of one component: running state, output buffers, last error and events.
  It keeps the output of a component up to 2 Mi characters. When the output is longer, it keeps the last half.
- `ComponentItemsLoaderHelper`: a list of `ComponentHelper` objects for installed components with an existing executable.
  `Init` replaces the full list. It does not change the list in place.
- `ComponentHelper`: the executable path, the folder and the startup parameters of one component.
  The executable is `<Directory>\<Executable>.exe` from the database. If it is missing, the helper tries `Store\Items\<id>`.

### How a component task starts

1. The tray sends `CONPTYMessageIds.GetStartupString` with `componentId`.
   For autorun, it sends `GetAllStartupStrings`. A conditional launch can send `StartComponent` or `ToggleComponent`.
2. `CoreCommandsHandler` calls `ComponentTasksManager.CreateAndRunNewTask(id)`.
   If the task exists, the manager stops it first (`StopProcessId`).
3. `ProcessService.StartProcess` reads the selected preset and makes the startup string (see "Presets").
4. An exception occurs when the executable is missing or the startup string is empty. The task then shows an error.
5. `SetupProxy` sends proxy packets. For ByeDPI, NoDPI and SpoofDPI, it can also replace the address and port (see "Proxy").
6. For NoDPI, the service adds `--quiet` one time.
7. The service sends `CONPTYMessageIds.StartProcessId` with `componentId`, `exePath` and `args`.
8. The tray starts the process and sends `MarkProcessIdAsStarted`, `ProcessIdNewOutput` and `MarkProcessIdAsStopped`.

`RunAllPreferredActions` starts each component that has `CONFIGS/<id>/usedForAutorun` set to `true`.

### Presets (`ConfigItem`)

A preset pack is a Store item of the type `configlist`. Its folder contains:

- `init.json` (`ConfigInitItem`): `toggleListAvailable` and `localized_strings_directory` (language code to file, for example `"EN"`).
- One `.json` file for each preset (`ConfigItem`). All `.json` files except `init.json` are presets.

Important `ConfigItem` fields:

| Field | Content |
|---|---|
| `target` | The first value is the component id. When the loader gets `$ANY` as the target, it loads all presets. |
| `name` | The display name. Each `$WORD(key)` in the name is replaced with `key` from the localization file of the pack. |
| `startup_string` | The command line with `%NAME%` variables and LScript functions. |
| `variables` | A list of `"%NAME%=value"`. A value can be an LScript expression, for example `$LOCALCONDITION(...)`. |
| `jparams` | Boolean switches that `$LOCALCONDITION` reads. |
| `commaVars`, `availableCommaVarsValues` | Variables with a list of possible values. |
| `configMaker` | Editor data. The component start does not read it. `UsedFiles`, `ExpandVariables` and `ExpandFileReference` read it. |

The selected preset is in the settings `CONFIGS/<componentId>/configId` (the pack id) and `configFile` (the file name).
The Zapret2 component also loads Zapret (`CSZTBN012`) presets. These presets have `IsLegacy = true`.

`ConfigurationService.GetStartupParametersByConfigItem` makes the startup string in this sequence:

1. If `jparams`, `variables` or `startup_string` is `null`, it returns an empty string.
2. `GetReadyToUseVariables` runs `LScriptCore.ExecuteScript` on each variable value (`callItemId` is the pack id).
   A value that starts with `$GETCURRENTDIR` or `$GETSRDIR` stays as it is. Step 5 expands it and quotes the path.
   Names are not case-sensitive. When a name occurs two times, the last value is used.
3. `ReplaceVariables` replaces `%NAME%` (letters, digits, `_`, `.`, `-`). Values can refer to other variables.
   It does a maximum of 16 passes and stops at 64 KiB. Unknown names stay.
4. `ReplaceCommaVariables` replaces `commaVars` with the same rules.
5. `LScriptCore.ExecuteScriptUnsafe` expands LScript functions in the full string (`quotePaths: true`).
6. For a legacy preset, `Zapret2LegacyConfigService` converts the result for winws2.

The legacy conversion keeps a cache in `Store\Items\__ConvertedZPRT`. The setting
`ZAPRET2/validateLegacyCacheHashes` (default `true`) makes it compare file hashes before it uses the cache.

Other configuration types:

- `ConfigItem.ExpandVariables`, `ExpandFileReference`, `ResolveFilePath` and `UsedFiles`: expand variables and `preset://` aliases without LScript. AddOns (for example ConfigShare) use them.
- `ConfigCommandLine`: tokenizes and quotes command lines. It does not run them.
- `ConfigFileReferences`: finds and rewrites file arguments in a command line.
- `ConfigPackMakeHelper.CreateConfigPack`: copies presets, used files and localization files into a pack. It writes a ZIP file, or registers the pack in the database (`autoImport`).
- `ConfigurationService.LoadConfigFromFile`: reads a `.json` preset, an old preset with `custom_parameters`, or a `.bat`/`.cmd` file.
- `Autocorrector.FindAutoCorrectPath`: finds a file of a preset in the pack, the component and other installed items.

## Features

- `ApplicationAutorunManager`: sends `AddToAutorun` or `RemoveFromAutorun` to the tray and sets `SYSTEM/autorun`.
- `ApplicationUpdate`: checks the application release on GitHub or GitLab. After the Store installs the application item, it sends `BeginApplicationUpdate` to the tray with `patch.cdpipatch` (portable) or `patch.msi` (MSI).
- `FileSystemLinksManager`: creates symbolic links or hard links for item files. It creates the link with a temporary name first.

## JSON

`JSONConvertor` loads, serializes and deserializes JSON with Newtonsoft.Json.

## LScript

LScript is a small expression language in Store metadata, preset variables and startup strings.
See [LScript/README.md](LScript/README.md).

## Proxy

- `ProxyHelper`: `ProxySetupTypes` (`None`, `AllSystem`, `ProxiFyre`, `NoActions`, `AsInConfig`) and `ProxyLikeComponents` (ByeDPI, NoDPI, SpoofDPI).
  `ReplaceArgsForProxy` removes the address and port options and adds the values of `PROXY/IPAddress` and `PROXY/port`.
  `SetupProxy` uses it only when `PROXY/proxyType` is `AllSystem`, `ProxiFyre` or `NoActions`.
  `IsLoopbackAddress` and `IsValidPort` validate user input.
- `WindowsNativeProxySettingsService`: reads and writes the Windows Internet Settings proxy values in the registry.

`ProcessService.SetupProxy` uses `PROXY/proxyType`. `ProxiFyre` needs the add-on `ASPEWK002`.

## Security

`CertificateCheck.CheckCatalog` verifies a signed `catalog.cat` against a folder.
It uses `CatalogVerifier` from `CDPIUI.Shared` and `Secret.TrustedCertificates`.

## Store

`StoreHelper` is the facade for the Store. The GUI uses this class. The other Store services are internal.
The catalogue format, the download types and the checks for each type are in [Store/README.md](Store/README.md).

| Type | Responsibility |
|---|---|
| `StoreHelper` | Install, update, remove and update checks. Events for progress and errors. |
| `Queue/QueueManagerService` | A FIFO queue. It installs one item at a time. It also keeps the list of failed items. |
| `Repository/RepositoryLoaderService` | Downloads the Store repository (GitHub or GitLab) and reads categories, items and ready kits. |
| `Database/DatabaseHelper` | The SQLite table `Items` in `storedata.db`. One row for each installed item (`DatabaseStoreItem`). All queries, also `RestorePaths`, read the table under one lock. At start, the GUI calls `RestorePaths`. If the GUI cannot restore an item, it registers the user item or the application item again. For other items, `UnregisterMissingItem` deletes the row when the item folder is missing (`CDPIUI/Helper/Database/DatabaseInitializationService.cs`). |
| `Network/APIWorker`, `RequestWorker`, `DownloadWorker` | Release API requests, HTTPS downloads, archive extraction and MSI start. `RequestWorker` sends a Store token only to GitHub and GitLab hosts. |
| `MSI/MsiInstallerService`, `MsiInstallerQueueManager` | Send an MSI to the tray for installation and wait for the result (maximum 30 minutes). |
| `Application/ApplicationUpdateService` | Prepares an application patch (`patch.cdpipatch`) and its required patches. It reads the version of each required patch from its own `requirements.json` and skips versions that are not newer than the installed version or not older than the main patch. It puts the layers in version sequence, the oldest first. See [.github/RELEASES.md](../.github/RELEASES.md#gui-side). |
| `LocalItemsInstallerHelper` | Reads local pack files and adds them to the queue. |
| `StoreItemIds` | Validates item ids (`^[A-Za-z0-9_-]{1,64}$`) and item folders. |
| `VersionHelper` (`VersionComparier.cs`) | Compares version strings. |
| `Data/HardcodedItemIds` | Fixed ids of components and add-ons. |

### Store repository cache

`LoadAllStoreDatabase` downloads the repository archive when the cache is missing.
It also downloads the archive when a sync is forced and the last sync (`STORE/lastSyncTime`) is one day old or more.
The library extracts the archive into a new folder `Repo.new-<GUID>`. The library replaces `Store\Cache\Repo` only after a full extraction.
If GitHub fails, the library tries the GitLab mirror.

### How an item is installed or updated

1. `AddItemToQueue` adds the item with a new operation id. An item can be in the queue one time only.
2. `InstallItemWorker` validates the item id.
3. If the item is installed, the library stops the component and its driver service. It keeps the running state.
4. The library creates `Store\Items\<id>`. It deletes the old content only when `cleanDirectoryBeforeInstalling` is `true`.
5. An online item is downloaded through HTTPS to `TempFiles\Downloads`. The library then extracts or copies it into the item folder.
6. A local pack is moved into place (see "Staging and rollback").
7. `AddItemToDatabase` runs `before_install_actions`, writes the database row, and runs `after_install_actions`.
   The two action lists get the item folder as `CurrentDirectory`. The library downloads the `DOWNLOAD=` requests of the two lists (see [LScript/README.md](LScript/README.md)).
   For a local pack, the library keeps a `$DYNAMICIMAGE(...)` icon as a full path in the item folder (`GetIconPath`).
8. If the component ran before the update, the library starts it again.
9. `ItemActionsStopped` makes `ComponentTasksManager` load the component list again.

If a first installation fails, the library removes the item. If an update fails, the database row of the installed version stays.

### Staging and rollback

| Content | Behaviour |
|---|---|
| Store repository | Extracted to `Repo.new-<GUID>`, then moved into place. |
| Signed archive (`signedZip`, `.cdpisignedpack`) | Extracted to `<target>.verify-<GUID>`. The library moves the files into place only when the catalog is valid. |
| Local pack | The old folder moves to `<folder>.previous-<GUID>`. If the move of the new pack fails, the old folder comes back. |
| Online, not signed archive | Extracted directly into the item folder. There is no rollback. |

CAUTION: Do not expect a rollback for online updates. A failed online update can leave new files next to old files.

An archive that is not signed merges `.txt` files: the library adds new lines to an existing file and does not replace it.
A pack that is not signed (not `.cdpisignedpack`) cannot bring `.exe`, `.dll`, `.sys`, `.msi`, scripts or links.
For such a pack, the library ignores `ExecutableFile`, `BeforeInstallActions` and `AfterInstallActions`.

## System

- `ZipService` (internal): ZIP extraction with a root folder filter (`$ANY` matches one segment), skip patterns and the catalog check.
- `ZipService` and the Store repository download use `ArchivePaths` from `CDPIUI.Shared`: entry paths must stay in the target folder, the archive must be within the size limits, and links below the target are rejected.
- `BatchFileEncoding`: reads `.bat` files in UTF-8, UTF-16 or a Cyrillic code page.
- `ShellHelper`: opens folders and files and starts applications.

## Tests

Run `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj`. The tests of this library are in `CDPIUI.Tests/Core`.
