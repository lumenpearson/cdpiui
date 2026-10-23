# Update

`Update.exe` is a .NET Framework 4.7.2 WinForms tool. It installs an application patch
(`.cdpipatch`) into the installation folder. Then it starts the tray again.

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

The [CI and release documentation](../.github/README.md#how-the-application-applies-a-patch)
gives the full check procedure.

Notes on the arguments (`Update/Program.cs`):

- `--directory-to-zip` is the path of the ZIP package, not of a directory.
- The tool removes a trailing `"` from each value. A quoted path that ends with `\` causes this character.
- If one of the two arguments is missing, the tool shows an information message and stops.

The project does not embed a manifest. Thus `Update.exe` gets the rights of the tray.

## Procedure

`Update/InstallHelper.cs` contains all the logic. The procedure starts when the window opens.

1. **Stop the application.**
   - The tool sets the `Global\CDPIUI_TrayShutdown` event. The tray then exits as from its menu: it stops the components and restores the proxy settings.
   - The tool waits a maximum of 20 s for the tray.
   - The tool kills all remaining `goodbyeDPI`, `CDPIUI` and `CDPIUI_TrayIcon` processes.
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
   - The tool also moves files to the backup folder if the previous manifest lists them and the update does not contain them. This removes these files.
4. **Roll back after a failure.**
   - If a move fails, the tool restores the applied files from the backup folder in reverse sequence.
   - If the restore is incomplete, the status becomes `NoOldVersion`.
   - In that case, an error message tells the user to install the application again. The tool keeps the backup folder for manual recovery.
5. **Finish the update.**
   - After a successful update, the tool writes the manifest.
   - The tool deletes the staging and backup folders.
   - The tool starts the tray.

### Skipped files

The tool does not stage `Update.exe` or `catalog.cat` if a file with the same name exists at the
destination. The manifest procedure never removes these two files.

### Manifest

`<install>\.update-manifest` is a text file. Each line contains one relative path. The file lists
the files that the last successful update wrote. The tool uses the manifest only to find the files
to remove at the next update. If the manifest is missing, the tool removes no files. For example,
an MSI installation has no manifest.

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

The MSI uninstall cleanup deletes `update.log`, `.update-manifest`, `.update-staging` and
`.update-backup` (`CDPIUI.TrayIcon/Helper/UninstallHelper.cs`).

## Window

The "Update Assistant" window (`Update/Form1.cs`, `Update/Form1.Designer.cs`) has a fixed size
and no control box. It contains these controls:

| Control | Content |
|---------|---------|
| Progress bar | Goes from 0 % to 100 % during extraction. Then it goes from 0 % to 100 % again during the apply step. |
| Status line | `Preparing...`, `Unpacking...`, `Copying files...`, `Finalizing...`, the current file, `Complete` or `Exception happens`. |
| Button | Disabled until the procedure completes or fails. Then it closes the tool. |

The user cannot close the window while the update runs (`FinishStatus` is `None`). After the
tray starts successfully, the tool closes automatically.
