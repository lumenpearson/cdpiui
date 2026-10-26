**English** | [Русский](README.ru.md)

# HostsFileEditor add-on

## Purpose

The HostsFileEditor add-on adds the CDPI UI domain block to the Windows hosts file and removes it. The GUI operates without administrator rights. Thus the add-on does not change the hosts file itself. It starts the separate tool `EditHostFile.exe` with administrator rights and waits for its exit code.

`EditHostFile.exe` contains all the logic of the hosts file changes: the managed block, the backup, the encoding and the safe write. Refer to [EditHostFile/README.md](../../EditHostFile/README.md).

## Main type

`HostsFileEditService` (`HostsFileEditorService.cs`, namespace `CDPIUI.AddOns.HostsFileEditor`) is a class with static methods only.

| Method | Tool argument | Function |
|---|---|---|
| `AddDomains()` | `/add` | Adds the CDPI UI block and writes `hosts.bak` |
| `RemoveDomains()` | `/remove` | Removes the CDPI UI block |
| `RestoreDomains()` | `/recover` | Removes the CDPI UI block and deletes `hosts.bak` |

Each method returns `Task<int>`: the exit code of the tool.

## Sequence

1. The method finds `EditHostFile.exe` and `CDPIUI_ElevatedLauncher.exe` in the application folder (`Directories.CurrentDirectory`).
2. If one of the files does not exist, the method throws `ApplicationFilesDamagedException`.
3. The method starts `CDPIUI_ElevatedLauncher.exe hosts /<argument>` with `UseShellExecute = true` and the verb `runas`. Windows shows the UAC prompt for the launcher.
4. The launcher removes the .NET variables of the user profile, starts `EditHostFile.exe` with the same token and waits for it. Refer to [CDPIUI.ElevatedLauncher/README.md](../../CDPIUI.ElevatedLauncher/README.md).
5. The method waits for the launcher to exit.
6. The method returns the exit code. The launcher gives the exit code of the tool. If `Process.Start` returns `null`, the method returns `-1`.

If the user cancels the UAC prompt, `Process.Start` throws an exception. The method does not catch it. The caller must process it.

The exit codes of the tool are:

| Code | Meaning |
|---|---|
| `0` | Success |
| `0x80070057` (`E_INVALIDARG`) | No known argument |
| `0x80070002` | The launcher did not find `EditHostFile.exe` |
| Other | The `HResult` of the exception that stopped the tool, or `1`. A `0x8007xxxx` code can also come from the launcher, when Windows did not start the tool. |

## GUI

| Item | Location | Function |
|---|---|---|
| `EditHostFileWindow` | `CDPIUI/Windows/EditHostFileWindow.xaml(.cs)` | The window of the tool |
| `Views.EditHostsFile.MainPage` | `CDPIUI/Views/EditHostsFile/MainPage.xaml(.cs)` | Buttons for add, remove, recover and open |
| `HostsFileEditHelper` | `CDPIUI/Helper/AddOns/HostsFileEditHelper.cs` | Opens the hosts file in a text editor |

These items open the window:

- the hosts file card on the tools page (`CDPIUI/Views/UtilsPage.xaml.cs`);
- the route `cdpiui://Tools/Hosts` (`CDPIUI/Commands/CommandUriMapper.cs`).

The page converts the exit code or the exception to an error text with `ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", ...)`. The result `HOSTEDIT_SUCCESS (0x00000000)` is a success. The page does not show an error for a result that starts with `HOSTEDIT_OPERATION_CANCELLED_BY_USER`. For other results, the page shows an error dialog.

The "Open" card calls `HostsFileEditHelper.EditHostsFile`. The helper opens `%WINDIR%\System32\drivers\etc\hosts` with a UAC request. Notepad is the default application. The settings group `FILEOPENACTIONS` controls the application selection. If `isDialogShown` is `false`, or if `doNotRemindAgain` is `false`, `EditSitelistAskApplicationContentDialog` asks which application to use first.

The help button opens `cdpiui://Help/Utils/EditHostsFileUtility/`.

## Settings

The page keeps the state in the CDPI UI settings, group `HOSTS`:

| Key | Type | Set to `true` | Set to `false` |
|---|---|---|---|
| `isFileEdited` | `bool` | After a successful `/add` | After a successful `/remove` or `/recover` |
| `isBackupAvailable` | `bool` | After a successful `/add` | After a successful `/recover` |

The page uses these values to enable the cards:

- "Add" is enabled when `isFileEdited` is `false`.
- "Remove" is enabled when `isFileEdited` is `true`.
- "Recover" is enabled when `isBackupAvailable` is `true`.

## Limits

- The page does not read the hosts file to find its state. It uses only the `HOSTS` settings. A change of the hosts file outside CDPI UI does not change these settings. For example, the MSI uninstall cleanup runs `EditHostFile.exe /remove` but does not change the settings.
- Each operation shows a UAC prompt. The prompt shows `CDPIUI_ElevatedLauncher.exe`, not `EditHostFile.exe`.
- The add-on does not check a signature of `EditHostFile.exe` or of the launcher. In an MSI installation, only administrators can change the installation folder. A portable copy is in a folder that the user can change, together with `CDPIUI.exe` and the tray. Thus a check of one file does not protect it. The UAC prompt shows the path of the launcher.
- The add-on has no tests in `CDPIUI.Tests`.
