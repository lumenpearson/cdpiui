**English** | [Русский](README.ru.md)

# CDPIUI.TrayIcon

`CDPIUI.TrayIcon` is the background process of CDPI UI (`CDPIUI_TrayIcon.exe`).
It is a WinForms application for `net8.0-windows10.0.17763.0`, x64.
`app.manifest` sets `requireAdministrator`, so the tray always runs elevated.
The GUI runs as the desktop user and sends commands to the tray through a named pipe.
The pipe protocol is in [CDPIUI.Shared/Pipe/README.md](../CDPIUI.Shared/Pipe/README.md).

## Responsibilities

- Show the notification area icon and its menu.
- Start, stop and restart the components (Zapret, ByeDPI, GoodbyeDPI and others) through ConPTY.
- Stop the components when the tray process ends.
- Set and restore the system proxy.
- Add the tray to autorun and remove it.
- Run Conditional Launch tasks.
- Show toast notifications.
- Install MSI and EXE packages and application updates.
- Run GoodCheck and grant directory access for the GUI.
- Clean up before the MSI package uninstalls the application.

## Startup

`Program.cs` (`Programm.Main`) does these steps:

1. If the arguments contain `--uninstall-cleanup`, it runs `UninstallHelper.Cleanup` and exits.
2. It gets the mutex `Global\CDPIUI_TrayInstance`. It waits up to 10 seconds. If another tray has the mutex, it exits. An abandoned mutex is accepted.
3. It deletes the `.staging` directory (`PackageStaging.CleanUp`).
4. It restores a proxy backup that a previous tray left (`RegeditHelper.RestoreLeftoverSnapshot`).
5. It writes a new pipe key to `.session\pipe.key` (`PipeSessionKeyWriter`, see [Pipe](../CDPIUI.Shared/Pipe/README.md)). Then it starts the pipe server.
6. If a toast notification started the process, it creates `TrayApplicationContext`, handles the toast and does not do steps 7–11.
7. If the arguments request it, it starts the GUI as the desktop user (see the table below).
8. It renames `_Update.exe` to `Update.exe`, if `_Update.exe` exists.
9. It shows the notifications for update failure and for `--autorun`.
10. After 30 minutes, it asks the GUI to check for updates, if `NOTIFICATIONS/appUpdates` is `true`.
11. After 60 minutes, it asks the GUI to start the compatibility check, if `NOTIFICATIONS/compatibilityCheck` is `true`.
12. It creates `TrayApplicationContext`: the icon, the uninstall listener, the Conditional Launch engine and the notification handler.

If the pipe server cannot create the pipe and another tray process runs, the tray stops its own process.

### Command-line arguments

| Argument | Effect |
|---|---|
| `--uninstall-cleanup` | Runs the uninstall cleanup and exits. |
| `--autorun` | Asks the GUI for the startup strings of all autorun components (`GetAllStartupStrings`). It tries again each 5 seconds until the send succeeds. It shows the "minimized to tray" notification if `NOTIFICATIONS/trayHide` is `true`. |
| `--show-ui` | Starts the GUI. |
| `--gui-args:<data>` | With `--show-ui`: the arguments for the GUI. `GuiActivationArguments` decodes them. |
| `--gdpiui-migration=<data>` | Starts the GUI with this argument. |
| `--after-patching` | Starts the GUI. If `_Update.exe` cannot be renamed, it shows an update failure notification. |
| `--after-failed-update` | Starts the GUI and shows an update failure notification. |

The GUI starts the tray with `--autorun` or `--show-ui` when the GUI cannot connect to the pipe.
The autorun task starts the tray with `--autorun`.

## Folders

| Folder | Content |
|---|---|
| `ConditionalLaunch/` | `ConditionalLaunchEngine` (triggers) and `ConditionalActionExecutor` (actions) |
| `Controls/` | Custom WinForms controls of the tray menu |
| `Forms/` | `EmptyForm` (hidden window that owns the icon), `TrayMenuForm` (the menu) |
| `Helper/` | `ProcessManager`, `TasksHelper`, `PipeHelper`, `MsiInstallerHelper`, `PackageStaging`, `TrustedPaths`, `ExecutableTrust`, `UninstallHelper`, `GoodCheckProcessHelper`, `StartupActionDispatcher`, `Utils` |
| `Helper/Basic/` | Pipe server, pipe access (`PipeAccessPolicy`), pipe key (`PipeSessionKeyWriter`), command handler, ConPTY, job object, run helper, registry, notifications, logger, error helper, taskbar |
| `Helper/Locale/` | `LocaleHelper`: tray strings in English and Russian, component names. The language is the language that the user selected in the app (`SYSTEM/language`). If this value is missing or unknown, the Windows display language is used |
| `Helper/Settings/` | `SettingsManager` (`XMLSettingsService`), `AutoStartManager` |
| `Assets/` | Icons (embedded resources) |

## Components and ConPTY

- `TasksHelper` is a `TasksManageService<ProcessManager>`. Each component id has one task.
- `GetProcessIdFullOutput` and `GetProcessIdState` apply to the task of the `componentId` in the message. If `componentId` is empty, they apply to all tasks. `GetAllProcessStates` applies to all tasks. `TasksHelper.SendTasksOutput` and `TasksHelper.SendTasksState` read a snapshot of the task list.
- `ProcessManager` keeps the executable and the arguments of the last start. A restart uses them again, unless the GUI sent `ProcessIdStartupArgsChanged`. In that case, the tray sends `GetStartupString` to the GUI.
- `ConPTYHelper` starts the process in a pseudo console with `CreateProcessW` and sends the output to the GUI.
- `ConPTYHelper` changes known output text to a `PrettyErrorCode`. A specific code has priority over `UNKNOWN`. A later `UNKNOWN` match does not replace a specific code.
- `ChildProcessJob` puts each component into one job object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. Windows stops the components when the tray process ends, also after a crash.
- GoodCheck runs in its own kill-on-close job (`GoodCheckProcessHelper`). The tray stops all components before it starts GoodCheck. The tray stops GoodCheck when the GUI disconnects.
- `StopService` stops the components that use WinDivert (`CSGIVS036`, `CSZTBN012`, `CSZTBN062`). Then it runs `sc stop WinDivert` and `sc delete WinDivert`.
- When the computer resumes, the tray waits up to 30 seconds for the network. Then it restarts the components that were running.
- On exit and on session end, the tray stops all components. It waits up to 5 seconds.

### Messages to a closed GUI

If the tray cannot send a message, `PipeHelper.TrySendMessage` starts `<tray directory>\CDPIUI.exe` as the desktop user with `--direct:<message>`.
This applies to all message targets. For background messages, the tray also adds `--create-no-window`.
See [CDPIUI.Shared/Pipe/README.md](../CDPIUI.Shared/Pipe/README.md#messages-when-the-gui-is-not-connected).

## Proxy

`ProxyMessageIds.Setup` sets the proxy for a component. `proxyType` can have two values:

- `ProxiFyre`: the tray starts ProxiFyre (from `Init`) in a second ConPTY.
- `AllSystem`: the tray writes `socks=<ip>:<port>` to the Internet settings of the desktop user.

The tray writes to `HKEY_USERS\<desktop user SID>` when the desktop user is not the tray user. Otherwise it writes to `HKEY_CURRENT_USER`.

Backup and restore:

1. Before the first change, the tray reads `ProxyEnable`, `ProxyServer` and `ProxyOverride`.
2. It writes this snapshot to `proxy-backup.json` in the tray directory.
3. When the component stops, the tray restores the snapshot and deletes the file.
4. If the tray did not restore the snapshot (crash, kill, shutdown), the next tray start restores it.

When one component starts a proxy, the tray stops the other components that use a proxy.

## Autorun

`AutoStartManager` creates the Task Scheduler task `CDPIUI_Autostart`:

- Trigger: logon of the current user.
- Action: the tray executable with `--autorun`.
- Run level: highest. Logon type: interactive token.
- No execution time limit. The task also runs on battery power.

If `AddToAutorun` fails, the tray sends `Settings/AutorunFalse` to the GUI and shows a notification.

## Conditional Launch

`ConditionalLaunchEngine` reads the `.cdpitask` files from the tasks directory.
The default directory comes from the settings file path (`ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile`).
The GUI can send a different directory in `ReloadTasks`. The tray accepts it only if `TrustedPaths.IsAllowed` accepts it.

- A `FileSystemWatcher` reloads the tasks when a file changes. After a watcher error, the engine creates a new watcher.
- Hot key triggers use `RegisterHotKey` on the `EmptyForm` window.
- Process triggers poll the process list each second.
- When more than one task matches, only the tasks with the highest priority run.
- Actions that need the GUI (`ApplyPreset`, `StartComponent`, `ToggleComponent`, `StartAutorunComponents`, `CheckStoreUpdates`, `CheckApplicationUpdates`) go to the GUI as `ExecuteAction`. The tray waits 45 seconds for the result.
- The tray does the other actions itself, or it opens a GUI window.

## Notifications

`NotifyHelper.ShowMessage` shows a toast notification with an `action` argument.
`HandleToastActionFromBackground` handles the action when the user clicks the toast:

| Action | Effect |
|---|---|
| `SHOW_MAIN_WINDOW` | Opens `MainWindow`. |
| `OPEN_PSEUDOCONSOLE(<id>)` | Opens `ViewWindow` for the component. |
| `OPEN_PROXY_SETUP` | Opens `ProxySetupUtilWindow`. |
| `OPEN_BEGIN_STORE_UPDATE_CHECK` | Opens the Store download page. |
| `UPDATE:OPEN_DOWNLOAD_PAGE` | Opens the update page of `MainWindow`. |
| `LOGGER:OPEN_TRAY_LOG`, `LOGGER:OPEN_MSI_LOG`, `UPDATE:OPEN_LOG` | Opens a log file. |

Component start and stop notifications show only if `NOTIFICATIONS/procState` is `true`.

## Packages and updates

### MSI and EXE packages

`MsiInstallerHelper` installs packages one at a time from a queue (`MSIInstallation/Begin`):

1. The path must be accepted by `TrustedPaths.IsAllowed`.
2. `PackageStaging.Stage` copies the file to `<tray directory>\.staging\<GUID>\`.
3. The copy must have a valid Authenticode signature (`WinVerifyTrust`, no revocation check).
4. The tray runs `msiexec.exe /i "<copy>" /passive /norestart` for `.msi`. For other files, it runs `<copy> /install /passive /norestart`.
5. Exit codes 0 and 1638 give `Complete`. Exit codes 3010 and 1641 give `CompleteRestartRequest`. Other codes give `ExceptionHappens`.
6. The tray deletes the staging directory.

`MSIInstallation/Kill` stops the running installer or removes the operation from the queue.

### Application update

`Utils.StartUpdate` handles `Update/BeginApplicationUpdate`:

1. The path must be accepted by `TrustedPaths.IsAllowed`.
2. The tray copies the package to `.staging`.
3. For an `.msi` package, the copy must have an Authenticode signature of a certificate from `Secret.TrustedCertificates` (`PackageStaging.HasPinnedSignature`). The release workflow signs `Setup.msi` with the catalog certificate. This certificate is self-signed. Thus the tray accepts a chain to an untrusted root, but it does not accept a changed file or another signer. Then the tray runs `msiexec.exe /i "<copy>" /qn+`, closes the GUI and exits.
4. For a ZIP package, the tray checks the limits of `ArchivePaths` (number of entries, total size, compression ratio) and rejects links. Then it extracts the package and verifies the `catalog.cat` of each layer with `CatalogVerifier` and `Secret.TrustedCertificates`. Then it runs `Update.exe --directory-to-zip "<copy>" --destination-directory "<tray directory>"`.

## Elevated start of a program

The tray starts components (`ConPTYHelper`), the `--help` capture and GoodCheck with administrator rights. The GUI sends the path and the arguments. Before each start, the tray does these checks:

1. The path must be accepted by `TrustedPaths.IsAllowed`.
2. An option that writes a file (`--debug=@<file>`, `--wf-save`, `--hostlist-auto`, `--hostlist-auto-debug`) must point into the application directories (`FileWritingOptions`). The tray also reads the argument files (`@<file>`) of the command line, to a maximum of 4 levels.
3. In an MSI installation, for a program outside the installation folder, `ExecutableTrust.AcquireAsync` opens the program and each `.exe`, `.dll` and `.sys` file in its folder for reading. While these handles are open, no process can change, rename or delete these files.
4. This program must have one of these:
   - The same fingerprint as at an earlier approved start. The fingerprint is the SHA-256 of the names and the contents of the locked files.
   - A Store item catalog (`<item>\catalog.cat`) that has the signature of a pinned certificate and agrees with the code files. Data files (lists and other files) are not compared, because the user can change them.
   - The approval of the user. The tray shows a question with the path, the SHA-256 and the signer. Programs of the user cannot click its buttons, because Windows blocks input from a lower integrity level.
5. The tray saves the approved fingerprints in `<installation folder>\.trust\approved.txt`, one line per program: the fingerprint, a tab and the path. Only administrators can change this folder.

`ExecutableLease` (the locks and the fingerprint) and `ExecutableApprovals` (the approval file and the Store item folder) are in `CDPIUI.Shared/Security`, so `CDPIUI.Tests` can test them. `ExecutableTrust` in the tray decides and asks the user.
6. The tray releases the handles after the process starts.

A portable copy does not do steps 3 and 4, because the user can change the tray itself.

CAUTION: Do not start a program with administrator rights without `ExecutableTrust.AcquireAsync`.

## Uninstall cleanup

The MSI uninstaller runs `CDPIUI_TrayIcon.exe --uninstall-cleanup` as SYSTEM. `UninstallHelper.Cleanup` does these steps:

1. It sets the event `Global\CDPIUI_TrayShutdown`. Each running tray exits as if the user clicked Exit, so it stops the components and restores the proxy.
2. It waits up to 20 seconds for the trays to exit.
3. It stops the processes that `ApplicationPathMatcher` finds in the installation directory or in `\AppData\Local\CDPIUI\`.
4. It stops and deletes the `WinDivert*` services whose `ImagePath` belongs to the application.
5. It deletes the `CDPIUI_Autostart` task.
6. It runs `EditHostFile.exe /remove`.
7. It deletes `Logs`, `.update-staging`, `.update-backup`, `.old`, `.staging`, `.trust`, `.session`, `update.log` and `.update-manifest` in the installation directory.

The cleanup does not read the settings and does not write logs, because the SYSTEM profile is not the user profile.

## Settings and logs

- `SettingsManager` uses the first `Settings.xml` that exists:
  1. `<tray directory>\Settings\Settings.xml`
  2. `%LOCALAPPDATA%\CDPIUI\Settings\Settings.xml` of the desktop user
  3. `%LOCALAPPDATA%\CDPIUI\Settings\Settings.xml` of the tray user
- If no file exists, the tray selects the path of a new file:
  - For an MSI installation, the tray uses `%LOCALAPPDATA%\CDPIUI\Settings\Settings.xml` of the desktop user. The GUI of an MSI installation uses the same file.
  - In all other conditions, the tray uses item 1.
- The tray finds an MSI installation with the registry value `HKLM\Software\Storik4\CDPI UI\InstallLocation` (`Utils.IsInstalledByMsi`). The value must be equal to the tray directory. The GUI uses the same check (`CDPIUI.Core/State.cs`).
- The GUI sends `Settings/ReloadSettings` after it changes a setting that the tray reads.
- The tray writes logs to `<tray directory>\Logs\`. `DEBUG` builds write all levels. Release builds use `DEBUG/logLevel` (default `WARN`).

## Development

You must start the tray elevated and the GUI as a normal user. Some GUI features do not work when the GUI is elevated.

1. Create `CDPIUI.Shared/Secrets/Secret.cs` (see [CDPIUI.Shared/README.md](../CDPIUI.Shared/README.md#secrets)).
2. Start `CDPIUI.TrayIcon` as administrator. In Visual Studio, start Visual Studio as administrator.
3. Start `CDPIUI` as the normal desktop user.

Notes:

- In `DEBUG` builds, the pipe peer check compares only file names. Thus the GUI and the tray can run from different output directories.
- The tray starts only executables below its own directory or below `%LOCALAPPDATA%\CDPIUI` (`TrustedPaths`). To test components, run the GUI and the tray from the same directory. You can use the publish directory of `.github/scripts/build_portable.ps1`.
- The tray starts the GUI from `<tray directory>\CDPIUI.exe`. With different output directories, the tray cannot start the GUI.
- Two trays cannot run at the same time. Close the installed tray before you start a development build.

## Security rules

The tray is elevated, and the GUI is not. Treat each pipe message as data from a less trusted process.

- Accept pipe clients only after the peer check and the pipe key authorization (see the pipe README). The tray makes a new key at each start. An empty key authorizes no client. A failed authorization disconnects the client.
- Check each path from the GUI with `TrustedPaths` before you run, install or change it:
  - `IsAllowed`: below the tray directory, `%LOCALAPPDATA%\CDPIUI` of the tray user, or `%LOCALAPPDATA%\CDPIUI` of the desktop user.
  - `IsInStore`: below the `Store` directory of these roots. `GrantAccessRequest` uses it.
  - `TrustedPathValidator` rejects a path that has a symbolic link or a junction below the root.
- Copy a package to `.staging` before you check it. Check and run the copy, not the original file.
- Use only signed packages: Authenticode for installers, an Authenticode signature with a pinned thumbprint for `Setup.msi`, catalogs with pinned thumbprints for application updates.
- Start a program only through `ExecutableTrust.AcquireAsync`, and check its arguments with `FileWritingOptions` (see [Elevated start of a program](#elevated-start-of-a-program)).
- Limit an archive with `ArchivePaths` before you extract it.
- Use full paths for system tools (`msiexec.exe`, `sc.exe`, `powershell.exe`, `explorer.exe`, `notepad.exe`).
- Start processes with `UseShellExecute = false` (`RunHelper.Run`).
- Do not use `ShellExecute` from the tray to open a file or a URL. File associations come from the user, so the handler can run elevated. Use `RunHelper.RunAsDesktopUser`, as `NotifyHelper.OpenFileInDefaultApp` does with `explorer.exe`.
- Start the GUI with `RunHelper.RunAsDesktopUser`. It uses the token of the shell process, so the GUI does not run elevated.
- `GrantAccess` gives `Modify` access only to the tray user.

WARNING: Do not add a pipe handler that runs a file or changes access without a `TrustedPaths` check.
