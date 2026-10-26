**English** | [Русский](README.ru.md)

# CDPIUI.Shared

`CDPIUI.Shared` is a `netstandard2.1` library (C# 12, nullable enabled).
It contains the code that the GUI, the core and the tray use together.

These projects reference it:

- `CDPIUI` (GUI)
- `CDPIUI.Core`
- `CDPIUI.AddOns`
- `CDPIUI.TrayIcon`
- `CDPIUI.Tests`

Rules for all code in this library:

- The code must compile for `netstandard2.1`. The `System/` folder adds the attributes that C# 11 `required` members need.
- The elevated tray uses this code. Treat each path and file that comes from the GUI as data from a less trusted process.
- Package references: `Microsoft.CSharp` and `System.Security.Cryptography.Pkcs`.

Tests for this library are in `CDPIUI.Tests/Shared/`.
The pipe protocol is in [Pipe/README.md](Pipe/README.md).

## Root files

| File | Content |
|---|---|
| `SharedConstants.cs` | Store repository names, Store item ids, update URLs, `DatabaseFileName`, the URI scheme `cdpiui` (`Schema`), `IsPreview` |
| `Utils.cs` (`SharedUtils`) | `GenerateNewId`, `IsOsSupportedNewGlyph`, `GetRandomHexNumber` |

`GetRandomHexNumber` uses `RandomNumberGenerator`. Use it for secret values, not `System.Random`.

## Basic

| Type | Purpose |
|---|---|
| `XMLSettingsService` | Reads and writes `Settings.xml`. The GUI and the tray use it. |
| `CommandLine/GuiActivationArguments` | Encodes the GUI arguments as `--gui-args:`, so the tray can start the GUI with the same arguments. |
| `CommandLine/CommandLineTokens` | Splits a component command line into arguments as the Windows command line does. `ConfigCommandLine` in Core uses it. |
| `Filesystem/ArchivePaths` | Checks archive entries: the path must stay in the target folder, the archive must be within the limits (20000 entries, 1 GiB, compression ratio 200 for entries from 1 MiB), and no folder or file below the target may be a link. `ExtractToFileBounded` stops when an entry gives more data than its header says. |
| `Filesystem/TrustedPathValidator` | Checks that a path is in an allowed root directory and that no part below the root is a reparse point. |
| `Filesystem/ApplicationPathMatcher` | Finds the processes and drivers of the application during uninstall. |
| `Filesystem/DirectoriesManager` | Returns the executable directory if it is writable. Otherwise, or if the caller sets `forceAppData` (MSI installation), it returns `%LOCALAPPDATA%\CDPIUI`. |
| `Filesystem/FileSystemService` | File extensions, temporary names, `WriteAllTextAtomic`, file and directory sizes. |

### XMLSettingsService

The file has a `Settings` root element, `Group` elements with a `Name` and `Setting` elements with `Key`, `Type` and `Value`.

Load:

- DTD processing is prohibited, because the elevated tray reads a file that the user can change.
- A missing file causes a new empty document, and the service saves it.
- An unreadable file causes a load from `Settings.xml.bak`. The service keeps a copy of the bad file as `Settings.xml.corrupt-<GUID>`.
- If the backup is also unreadable, `HasUnrecoverableLoadError` is `true` and the service writes `Settings.xml.recovery-notice`.

Read:

- `GetValue` and `GetValueOrDefault` write the default value to the file when the key does not exist.
- A value of a different type returns the default value. It does not cause an exception.
- Default values: `false`, `"NaN"`, `0`, `10.0`, an empty list. Subclasses can override `GetDefaultValueForKey`.

Write:

- Each `SetValue` reads the file from the disk again, applies one change and saves the file. Thus a process with an old copy does not overwrite a change from the other process.
- A `Settings.xml.lock` file with `FileShare.None` locks the write.
- The service writes a temporary file and replaces `Settings.xml` with it. The previous file becomes `Settings.xml.bak`.
- Supported types: `int`, `double`, `bool`, `string`, `DateTime`, `List<string>`. `double` and `DateTime` use the invariant culture.
- `List<string>` items are separated by `;`. The service writes `;` as `%3B` and `%` as `%25`.
- `SetValue(group, ...)` raises `PropertyChanged`. `SetValue(groupPath, ...)` raises `EnumPropertyChanged`.
- `Reload` does not raise events.

## ComponentsTask

| Type | Purpose |
|---|---|
| `IProcessService` | The interface of a component process manager. The tray implements it in `ProcessManager`. |
| `TaskModel<T>` | A task: `Id`, `ProcessManager`, `IsSetupComplete`. |
| `TasksManageService<T>` | The task list. It adds, starts, stops and removes tasks and raises `TaskListUpdated`, `TaskStateUpdated`, `TaskSetupStateUpdated`. |
| `OutputCleanupHelper` | Removes escape sequences from pseudo console output. |

Rules:

- `TasksManageService` uses a `SemaphoreSlim` for the list. Use `GetTasksSnapshot` to read the list from more than one thread.
- `CreateAndRunNewTask` stops a task that exists before it starts it again.
- `OutputCleanupHelper` removes only sequences that start with ESC. Use `TakeIncompleteTail` to keep a sequence that is cut at the end of a chunk.

## ConditionalLaunch

`ConditionalTaskModels.cs` contains the task file format of the Conditional Launch feature.

- `ConditionalTask` is XML (`.cdpitask`), version 3. Version 1 stored one `Trigger` element. `Normalize` moves it to `Triggers`.
- Trigger types: `HotKey`, `ProcessStarted`, `ProcessStopped`.
- Action types: see `ConditionalActionType`.
- `ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile` returns `<data directory>\ConditionalTasks`. The data directory is the parent of the `Settings` directory.
- `LoadDirectory` skips files that are not valid and files with a duplicate `Id`. The files are sorted by name.
- `Validate` requires a GUID `Id`, a name and a priority from -1 to 1. It also requires one or more triggers, one or more actions and a delay from 0 to 86400 seconds.
- `Save` and `Export` write a temporary file and then replace the target file.

## Exceptions

Custom exceptions that `ErrorConvertor` changes to a `PrettyErrorCode`.

| Folder | Types |
|---|---|
| root | `ApplicationFilesDamagedException`, `AsyncOperationException`, `MsiInstallException`, `NewestVersionAlreadyInstalledException`, `UnknownException`, `UnknownFileFormatException` |
| `Catalog/` | `CatalogInvalid`, `CatalogNoSignature`, `CertificateNotTrusted` |
| `Database/` | `AddonNotInstalledException`, `ApplicationItemRegistrationException`, `DatabaseUserItemRegistrationException` |
| `Settings/` | `SettingNotFoundException`, `SettingTypeNotSupported` |
| `Interface/` | `ICustomException` |

An exception that implements `ICustomException` gives its own `PrettyErrorCode`.

## Extensions

| Type | Purpose |
|---|---|
| `EnumExtension` | `ToEnum<T>(string)` (case-insensitive, throws on an unknown value) and `ToEnum<T>(string?, T defaultValue)` |
| `DictionaryExtension` | `GetKeyByValue`, `AddRange` (adds only new items) |
| `StringExtension` | `SerializeTuples`, `DeserializeTuples`, `FirstCharToUpper` |
| `LogicalComparer` | Sorts `INamedModel` items with `StrCmpLogicalW` |

## Logger

`LoggerBase` writes log files. Each process has its own subclass with an `Instance`:

- The tray: `CDPIUI.TrayIcon/Helper/Basic/Logger.cs`. The root directory is the tray directory.
- The GUI and the core: `CDPIUI.Core/Basic/Logger.cs`. The root directory is `Directories.DataDirectory`.

Files and rotation:

- The file is `<root>\Logs\<sender>.log`. The service replaces characters that are not valid in a file name with `_`.
- When a file is larger than 5 MiB, the service copies it to `<sender>.old.log` and clears it.
- The file is opened with `FileShare.ReadWrite | FileShare.Delete`, because the GUI and the tray can write to the same file.
- The first line of a new file is `LoggerBase is ready-to-work <level>`.

Levels (`LogSelevirity`): `DEBG`, `INFO`, `WARN`, `CRIT`. `CreateErrorLog` writes `CRIT`.
The logger writes only messages at the minimum level or higher. Each message also goes to `Debug.WriteLine`.

WARNING: Do not write secrets (tokens, the pipe key) to the log.

## Migration

`GoodbyeDpiMigrationActivation` reads and creates the `--gdpiui-migration=` argument. GDPIUI-Updater uses it to start a migration from GoodbyeDPI UI.

- The value has six parts, separated by `.`:
  1. The version (`1`).
  2. The archive path (Base64 URL).
  3. The SHA-256 hash (lower case hex).
  4. The migration id (GUID, `N` format).
  5. The response pipe name (Base64 URL).
  6. The session token (32 to 64 bytes, Base64 URL).
- The archive must be a `.zip` file. The pipe name must start with `GDPIUI-Updater-`.
- The tray passes `RawArgument` to the GUI without a change.

## Models

| Type | Purpose |
|---|---|
| `OperationResultModel<T>` | A result with an `ErrorModel` |
| `UnprocessedOperationResultModel<T>` | A result with an `Exception` |
| `EmptyResult` | A result without a value |
| `IOperationResultModel`, `INamedModel` | Interfaces |

Each result has `Success`, `Result`, `ErrorHappens` and `Error`.
Use `SuccessResult`, `UnSuccessResult` (failed, no error) and `FailureResult` (failed with an error).

## Pipe

See [Pipe/README.md](Pipe/README.md).

## PrettyErrorConversionService

`ErrorConvertor` changes an exception to a `PrettyErrorCode` and to a text for the user.

- `MapExceptionToCode` examines the exception and each inner exception. It uses the exception type, socket errors, HTTP status codes and HRESULT values.
- Win32 codes and the related `0x8007xxxx` HRESULT give the same code.
- `GetPrettyErrorCode(prefix, ex, logger)` returns `<PREFIX>_<CODE>[_<HTTP status>] (0x<HRESULT>)`. The HRESULT part is present only when it is known. Example: `SAMPLE_PREFIX_IO_FILE_NOT_FOUND`.
- `GetErrorModel` returns an `ErrorModel` with `ErrorCode`, `Object`, `StatusCode`, `Exception` and `HResult`.
- Each method writes the error to the logger that you give it.

The prefix must be in upper case with `_` between words, for example `SAMPLE_PREFIX`.
To add a code, add it to `PrettyErrorCode.cs` and to the mapping in `ErrorConvertor.cs`.
The tray uses a subclass, `CustomErrorConvertor`, that always writes to the tray log.

## Secrets

`Secrets/Secret.cs` contains the build secrets. Git ignores this file.
You must create it before you build the solution.

1. For a local build, run `pwsh .github/scripts/create_secret_stub.ps1`. The script creates `Secret.cs` with empty tokens and an empty `TrustedCertificates` list. The script does not change a file that exists.
2. Alternatively, create a `public static class Secret` in the `CDPIUI.Shared.Secrets` namespace. Copy only the members of `SecretExample` in `SecretExample.cs`.

The release workflow writes `Secret.cs` with `.github/scripts/create_release_secret.ps1` from repository secrets. See [.github/RELEASES.md](../.github/RELEASES.md).

`SecretExample.cs` is excluded from the build in `CDPIUI.Shared.csproj`. The `ISecret` interface in it describes the members:

| Member | Effect of an empty value |
|---|---|
| `GitHubToken`, `GitLabToken` | The Store is not available. |
| `ProxyURL`, `ProxyKey` | No Store reverse proxy. |
| `TrustedCertificates` | Signed packages, signed patches and application updates cannot be verified. |

`TrustedCertificates` contains the SHA-1 thumbprints of the certificates that can sign catalogs and `Setup.msi`.

The application is public, so each value in `Secret.cs` is public too. Use only Store tokens that cannot change anything. `create_release_secret.ps1` refuses a GitHub token with scopes and a GitLab token with scopes other than `read_api`, `read_repository` and `read_registry`. The GUI sends a Store token only to GitHub and GitLab hosts (`RequestWorker`). An older `Secret.cs` can contain `AuthGuid`. The application does not use this member.

CAUTION: Do not commit `Secret.cs`. Do not use the stub values in a release build.

## Security

`CatalogVerifier` verifies a signed `catalog.cat` and the files of its directory.

1. The signature is decoded as CMS. Each signer certificate must be in the trusted thumbprint list. The certificates are self-signed, so the chain is not built.
2. Each signature must be valid (`CheckSignature(verifySignatureOnly: true)`).
3. `powershell.exe` from the system directory runs `Test-FileCatalog` with `-FilesToSkip catalog.cat` and the file names that the caller gives (`filesToSkip`). The paths and the names go through environment variables.
4. The result must be `Valid` in 2 minutes.

The result is a `CatalogVerification` value: `Success`, `NoSignature`, `NotTrustedSignature`, `FilesDoNotMatch` or `Failure`.

The certificates are pinned by thumbprint, and the chain, the revocation and the expiry date are not checked. This is intentional: the certificates are self-signed, and an expiry check would reject all packages that were signed before the expiry date. To replace a certificate, add the new thumbprint to `TrustedCertificates`, release this build, and only then sign with the new certificate. To revoke a certificate, remove its thumbprint and release a build.

`FileWritingOptions` finds component options that write a file (`--debug=@<file>`, `--wf-save`, `--hostlist-auto`, `--hostlist-auto-debug`) and checks that the file is in the allowed folders. It can also read the argument files (`@<file>`). The tray uses it before each component start, and `ConfigShare` uses it for shared presets.

`ElevatedLauncherCommand` gives the file name of `CDPIUI_ElevatedLauncher.exe`, its target names (`tray`, `hosts`) and its command line. The GUI and the autorun task of the tray start the tray and `EditHostFile.exe` with administrator rights only through the launcher. Refer to [CDPIUI.ElevatedLauncher/README.md](../CDPIUI.ElevatedLauncher/README.md).

## System

Polyfills for `netstandard2.1`:

- `System/Runtime/CompilerServices/RequiredMemberAttribute.cs`
- `System/Runtime/CompilerServices/CompilerFeatureRequiredAttribute.cs`
- `System/Diagnostic/CodeAnalysis/SetsRequiredMembersAttribute.cs`

Do not remove them. The `required` members in this library need them.
