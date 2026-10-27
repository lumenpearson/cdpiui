**English** | [Русский](README.ru.md)

# EditHostFile

`EditHostFile.exe` is a .NET 8 console tool. It adds or removes the CDPI UI block
in the Windows hosts file (`%WINDIR%\system32\drivers\etc\hosts`). Its manifest requires
administrator rights (`EditHostFile/app.manifest`). `EditHostFile/Program.cs` contains all the logic.

`EditHostFile.exe` is a framework-dependent single file, as the tray is. It uses the .NET 8
runtime that the tray also uses. Refer to [Build](#build).

## Arguments

| Argument   | Action |
|------------|--------|
| `/add`     | Removes the existing block. Writes `hosts.bak`. Adds the block from `Data/hosts` at the end. |
| `/remove`  | Removes the block. If there is no block, the tool does not write the file. |
| `/recover` | Removes the block. Then deletes `hosts.bak`. The tool does not copy the backup back. |

The tool uses only the first argument that it finds, in this sequence: `/remove`, `/add`, `/recover`.

## Missing hosts file

If the hosts file does not exist, the tool reads it as an empty file:

- `/add` writes an empty `hosts.bak`. Then it creates the hosts file with the block. The new file uses UTF-8 without BOM and `\r\n` line breaks.
- `/remove` finds no block. The tool does not create the file.
- `/recover` finds no block. The tool deletes `hosts.bak`, if it exists.

## Managed block

The block is the embedded resource `EditHostFile/Data/hosts`. Its first line starts with
`# [CDOM-B]`. Its last line starts with `# [CDOM-E]`.

To remove the block, the tool deletes all text from `# [CDOM-B]` to the end of the `# [CDOM-E]`
line. The tool also deletes the line break after it.

CAUTION: Do not put manual entries in the block. The next `/add` or `/remove` deletes them.

Rules for `/add`:

- The tool does not add a resource line if the same line exists in the file outside the block. This rule applies only to lines that are not comments and not empty.
- The tool always adds comment lines and empty lines.
- If the file does not end with a line break, the tool adds one before the block.

## Encoding and line breaks

The tool reads the file as bytes and keeps its encoding:

| File start | Encoding that the tool uses |
|------------|-----------------------------|
| UTF-8 byte order mark (BOM) | UTF-8 with BOM |
| UTF-16 LE BOM | UTF-16 LE with BOM |
| UTF-16 BE BOM | UTF-16 BE with BOM |
| No BOM, valid UTF-8 | UTF-8 without BOM |
| No BOM, not valid UTF-8 | ANSI code page of the system (for example 1251) |

The ANSI code page is the code page that `GetACP` returns. .NET 8 has no ANSI code pages of its
own, and `Encoding.Default` is always UTF-8. Thus the tool registers `CodePagesEncodingProvider`
(`Program.RegisterCodePages`, `Program.GetAnsiEncoding`).

The tool finds the line break type as follows:

- If the file contains `\r\n`, the tool uses `\r\n`.
- If the file contains only `\n`, the tool uses `\n`.
- If the file has no line breaks, the tool uses `\r\n`.

New lines get the line break type of the file. The tool does not change the existing text.

## Backup and replacement

Before a change, `/add` writes `hosts.bak` in the folder of the hosts file. The backup contains
the file without the CDPI UI block, in the same encoding.

For each write, the tool does these steps:

1. Write the new content to `hosts.cdpiui.tmp`.
2. Flush the file to the disk.
3. Replace the target with `File.Replace`. If the target does not exist, use `File.Move`.
4. Delete the temporary file if it remains.

Thus the hosts file is never half written.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success. This includes `/remove` when there is no block. |
| `0x80070057` (`E_INVALIDARG`) | The command line has no known argument. |
| Other | The `HResult` of the exception that stopped the tool. If the `HResult` is `0`, the code is `1`. |

The tool writes progress and errors to standard output with the prefix `[CDPI UI/EditHostFileHelper]`.
The messages are in English. If the Windows UI language is Russian, they are in Russian (`EditHostFile/Strings.cs`).

## Callers

| Caller | Call |
|--------|------|
| `HostsFileEditService` (`CDPIUI.AddOns/HostsFileEditor/HostsFileEditorService.cs`) | Starts `CDPIUI_ElevatedLauncher.exe hosts /<argument>` from the application folder with the `runas` verb. The launcher starts the tool without the .NET variables of the user profile and returns its exit code. The hosts editor page (`CDPIUI/Views/EditHostsFile/MainPage.xaml.cs`) converts the code to an error with the `HOSTEDIT` prefix. |
| MSI uninstall cleanup (`CDPIUI_TrayIcon.exe --uninstall-cleanup`, runs as SYSTEM) | Runs `EditHostFile.exe /remove`. Waits a maximum of 15 s (`CDPIUI.TrayIcon/Helper/UninstallHelper.cs`). |

## Build

`EditHostFile/EditHostFile.csproj` is an SDK-style project: `net8.0-windows`, x64, `win-x64`.
The project embeds `app.manifest` (`ApplicationManifest`) and `Data/hosts`
(resource `EditHostFile.Data.hosts`). The version (`1.0.0.0`) and the other assembly data are in
the project file.

```
dotnet publish EditHostFile/EditHostFile.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained false -p:PublishSingleFile=true
```

On Linux or macOS, add `-p:EnableWindowsTargeting=true`. `.github/scripts/build_portable.ps1`
uses the same command and copies `EditHostFile.exe` into the release layout.

CAUTION: Do not remove `requireAdministrator` from `app.manifest`. The tool writes to the system hosts file.

CAUTION: Do not start the tool elevated directly from the GUI. Start it through `CDPIUI_ElevatedLauncher.exe`. The tool is framework-dependent, so the .NET variables of the user profile (for example `DOTNET_ROOT`) act on it. Refer to [CDPIUI.ElevatedLauncher/README.md](../CDPIUI.ElevatedLauncher/README.md).

## Tests

`CDPIUI.Tests/Tools/EditHostFileTests.cs` tests the encoding detection, the line breaks, the add,
remove and recover operations, and the backup. The tests use files in `Path.GetTempPath()`, not
the system hosts file.
