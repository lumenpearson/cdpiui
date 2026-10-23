**English** | [Русский](README.ru.md)

# Update

`Update.exe` is a .NET 8 WinForms tool. It installs an application patch
(`.cdpipatch`) into the installation folder. Then it starts the tray again.

`Update.exe` is a framework-dependent single file, as the tray is. It uses the .NET 8 Desktop
Runtime that the tray also uses. The tool replaces the application files while it operates.
Thus it must not load a DLL from the installation folder. The single file contains all the code
of the tool. Refer to [Build](#build).

`Update.exe` cannot replace itself while it runs. Thus a patch contains a new updater as
`_Update.exe`. At the next start, the tray changes the name of `_Update.exe` to `Update.exe`
(`CDPIUI.TrayIcon/Program.cs`, `.github/scripts/make_patch.ps1`).

## Start

Only the elevated tray starts `Update.exe`. The sequence is as follows:

1. The GUI sends the `BeginApplicationUpdate` message with the path of the prepared patch
   (`CDPIUI.Core/Features/ApplicationUpdate.cs`).
2. The tray receives the message (`CDPIUI.TrayIcon/Helper/Basic/CommandsHandler.cs`) and calls
   `Utils.StartUpdate` (`CDPIUI.TrayIcon/Helper/Utils.cs`).
3. The tray copies the package to `<install>\.staging\<GUID>\`.
4. The tray verifies the signed catalog of each layer (`CDPIUI.TrayIcon/Helper/PackageStaging.cs`).
5. The tray starts `Update.exe` with this command line:

```
Update.exe --directory-to-zip "<staged .cdpipatch>" --destination-directory "<install folder>"
```

The [CI and release documentation](../.github/RELEASES.md#how-the-application-applies-a-patch)
gives the full check procedure.

Notes on the arguments (`Update/Program.cs`):

- `--directory-to-zip` is the path of the ZIP package, not of a directory.
- The tool removes the `"` characters at the start and at the end of each value. A quoted path that ends with `\` gives a trailing `"`.
- If one of the two arguments is missing, the tool shows an information message and stops.

The project has no manifest of its own. The .NET SDK embeds the default manifest (`asInvoker`). Thus `Update.exe` gets the rights of the tray.

## Procedure

`Update/InstallHelper.cs` contains all the logic. The procedure starts when the window opens.

1. **Stop the application.**
   - The tool sets the `Global\CDPIUI_TrayShutdown` event. The tray then exits as from its menu: it stops the components and restores the proxy settings.
   - The tool waits a maximum of 20 s for the tray.
   - The tool stops all remaining `goodbyeDPI`, `CDPIUI` and `CDPIUI_TrayIcon` processes (`Process.Kill`).
   - The tool waits a maximum of 10 s for each process name. If a process continues to run, the tool does not change files.
2. **Stage the files.**
   - The tool deletes the old `<install>\.update-staging` and `<install>\.update-backup` folders.
   - The tool extracts the package into a new `.update-staging` folder.
   - The tool reads the archive folders `layers/0/`, `layers/1/`, ... in sequence. The application makes packages with these folders.
   - If the archive has no `layers/` folders, the tool reads the `CDPIUI/` folder. A published patch has this folder.
   - A file in a later layer replaces the same file from an earlier layer.
   - The tool stops the update if an entry has a rooted path, a `:`, or a path outside the target folder.
   - The tool stops the update if the archive contains no files.
3. **Apply the files.**
   - The tool moves each staged file into the installation folder.
   - Before this, the tool moves the existing file to the same relative path in `<install>\.update-backup`.
   - The tool does not remove installed files. A patch is differential: it contains only the files that changed. Thus a file that is not in the patch stays in the installation folder.
4. **Roll back after a failure.**
   - If a move fails, the tool restores the applied files from the backup folder in reverse sequence.
   - If the restore is incomplete, the status becomes `NoOldVersion`.
   - In that case, an error message tells the user to install the application again. The tool keeps the backup folder for manual recovery.
   - The tool does not start the application. The window closes after the user closes the error message.
5. **Finish the update.**
   - The tool deletes the staging folder and the backup folder.
   - The tool deletes the `<install>\.update-manifest` file, if it exists. Earlier versions of the tool wrote this file.
   - The tool starts the tray.

### Skipped files

- The tool never stages a `catalog.cat` file from a patch. The catalog of a patch lists only the files of that patch. Thus it does not agree with the installation folder after the next patch.
- The tool does not stage `Update.exe` if `Update.exe` exists at the destination. A patch contains the new updater as `_Update.exe`.

## Tray restart

The tool starts `CDPIUI_TrayIcon.exe` from the installation folder with one of these arguments:

| Argument | Condition |
|----------|-----------|
| `--after-patching` | The update is successful. |
| `--after-failed-update` | The tool did not apply the update. The previous files are kept or restored. Causes: the application did not stop, the package is missing, or extraction or apply failed. |

The tray behavior (`CDPIUI.TrayIcon/Program.cs`):

- With either argument, the tray starts the GUI as the desktop user.
- With `--after-failed-update`, the tray shows the `UpdateFailure` notification.
- With `--after-patching`, the tray shows the same notification only if it cannot change the name of `_Update.exe` to `Update.exe`.

If the tray executable is missing or does not start, the tool shows an error message.

## Log

The tool adds lines to `update.log` in the folder of `Update.exe`, which is the installation
folder. Each line starts with `[yyyy-MM-dd HH:mm:ss]`. The tool ignores log write errors.

The MSI uninstall cleanup deletes `update.log`, `.update-manifest`, `.update-staging`,
`.update-backup` and other runtime files (`CDPIUI.TrayIcon/Helper/UninstallHelper.cs`).

## Window

The `Update Assistant` window (`Update/Form1.cs`, `Update/Form1.Designer.cs`) has a fixed size
and no control box. The window uses the font Microsoft Sans Serif 8.25 pt, which was the default
font of .NET Framework (`Application.SetDefaultFont` in `Update/Program.cs`). The default font of .NET 8 is larger.
The window is not DPI-aware, as before (`Application.SetHighDpiMode`). Windows scales it.
The window texts are in English. If the Windows UI language is Russian, they are in Russian (`Update/Strings.cs`). The log lines are always in English.

The window contains these controls:

| Control | Content |
|---------|---------|
| Progress bar | Goes from 0 % to 100 % during extraction. Then it goes from 0 % to 100 % again during the apply step. |
| Status line | `Preparing...`, `Unpacking...`, `Copying files...`, `Finalizing...`, the current file, `Complete` or `Exception happens`. |
| Button (`Cancel`) | Disabled until the procedure completes or fails. Then its text changes to `Close`, and it closes the tool. |

The user cannot close the window while the update runs (`FinishStatus` is `None`). After the
tray starts successfully, the tool closes automatically.

The tool shows an error message box for these errors: the rollback is incomplete, the tray does
not start, or an unexpected exception occurs. The installation thread sends the error to the UI
thread. The window owns the message box. Thus the message box cannot open behind the window.
When the user closes the message box, the tool stops.

## Build

`Update/Update.csproj` is an SDK-style project: `net8.0-windows`, Windows Forms, x64, `win-x64`.
The version (`1.1.0.0`) and the other assembly data are in the project file.

```
dotnet publish Update/Update.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained false -p:PublishSingleFile=true
```

On Linux or macOS, add `-p:EnableWindowsTargeting=true`. `.github/scripts/build_portable.ps1`
uses the same command and copies `Update.exe` into the release layout. The publish folder must
contain only `Update.exe`.

## Tests

`CDPIUI.Tests/Tools/UpdateInstallHelperTests.cs` tests the path checks, the layer sequence, the
staging rules (`catalog.cat`, `Update.exe`), the apply step and the rollback. The tests do not
use the window.
