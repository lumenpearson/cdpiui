# ConfigShare

A `.cdpiconfig` file is a ZIP archive with one preset and the resources that the preset uses. `ConfigShareService` exports, checks, extracts and installs these archives. `ConfigImportService` and `ConfigImportInstaller` send this format to `ConfigShareService`.

The archive does not contain scripts, and the service does not run code from it.

## Format v1

`manifest.json` contains `Version`, `Name`, `Developer`, `Config` (a `ConfigItem`) and `Resources`. Each resource has `Path`, `Length`, `Sha256` and `RewriteReferences`. The resources are in the folder `resources/`. A portable reference has the form `share://resources/...`.

The export puts resources into these folders:

| Source of the file | Archive path |
|---|---|
| The preset kit folder | `resources/preset/...` |
| The component folder | `resources/component/...` |
| Other locations | `resources/external/<volume hash>/...` |

The file list comes only from `ConfigItem.UsedFiles`. The export resolves each path with `ConfigItem.ResolveFilePath`. It uses the component folder and the preset kit folder. ConfigShare packages only this list and changes the related references in the preset. It copies the files, also component libraries, without changes. ConfigShare does not parse Lua and does not look for more dependencies.

`UsedFiles` does not contain the target of `--debug`, so the export does not package it. If a file from `UsedFiles` is missing, the export fails with `SHARE_FILE_MISSING`. Exception: if the list of `--hostlist-auto` or `--hostlist-auto-debug` is missing, the export packages an empty file for it. The export does not make a file in the preset kit. The component makes this list while it operates.

## Limits and checks

| Item | Limit |
|---|---:|
| Entries in the archive | 4096, including `manifest.json` |
| Resources in an export | 4095 |
| Uncompressed size, total and for one entry | 512 MiB |
| `manifest.json` | 4 MiB |
| Compression ratio of an entry larger than 1 MiB | 200 |

Before the installation, the reader checks:

- the format version (only `1`);
- that the name, the developer, `target[0]` and `startup_string` are not empty;
- that the archive has exactly the entries of the manifest and `manifest.json`;
- duplicate entry names (the comparison ignores the case);
- the declared size and the SHA-256 of each resource;
- the entry count, the total size and the compression ratio;
- safe relative paths.

A safe path starts with `resources/` and uses only `/`. It has no `:`, no `.` or `..` part, no part that ends with a dot or a space, no invalid file name characters and no reserved device names such as `CON` or `LPT1`.

The archive cannot contain filesystem links. The reader rejects symbolic link entries. `SafeDestination` also rejects a destination folder that contains a reparse point.

## Options that write files

The tray starts components with administrator rights. Thus a shared preset must not write files outside its package. After the reader replaces the `share://` references, `EnsureNoOutsideWrites` checks each text of the config. It looks for these options:

- `--debug=@<file>` (`--debug` with other values does not write a file);
- `--wf-save`;
- `--hostlist-auto`;
- `--hostlist-auto-debug`.

The value of such an option must be an absolute path in the extracted package folder. Otherwise, the reader fails with `SHARE_UNSAFE_OPTION`. A relative path is also rejected.

The export does the same check after it replaces the references. Thus the export fails with `SHARE_UNSAFE_OPTION` for a preset that the reader would reject.

`BlockCheckReportImportService` uses the same check for the preset line of an imported BlockCheck2 report. Refer to [BlockCheck2/README.md](../BlockCheck2/README.md).

## Lifecycle

`ExportAsync` and `ReadAsync` return a `ConfigSharePackage` that owns its temporary folder. Dispose the package after you copy the exported file, or after the import is complete or canceled.

Before a Windows share, call `RetainForSystemShare`. This writes the marker file `.retain-for-system-share`, and `Dispose` then keeps the folder. When the next export dialog opens, `CleanupPreviousSystemShares` deletes the marked folders. This also occurs after an application restart. The cleanup does not touch other imports and exports that are in progress.

The temporary folders are in `TempFiles`. Their names come from `FileSystemService.GetNewTempFileName` and contain `ConfigShare_` and a GUID. Thus names do not collide.

`InstallAsync` puts the resources into a unique folder `Shared/<GUID>` in the destination kit. Only one installation operates at a time. The destination can be:

- `LocalUserStorage` (the default);
- an existing kit with a GUID ID;
- a new kit with a GUID ID. The service makes the folder with `init.json` and registers a `configlist` item in the Store DB.

Store kit IDs are never valid destinations (`SHARE_DESTINATION_INVALID`). The config file name is `preset_<name>_<8 hex characters>.json`. If an error occurs, the service removes only the files and folders that this installation made. A second import of the same package makes a new preset. It never overwrites an existing preset or resource.

Dependencies that `UsedFiles` does not report must be attached to the preset as explicit resources.

## UI

| Entry point | Function |
|---|---|
| `ConfigSelector` (`CDPIUI/Controls/ComponentSettings`) | Opens the export dialog through `ConfigShareUI.ShowExportAsync` |
| `ConfigShareExportContentDialog`, `ConfigShareMessageContentDialog` (`CDPIUI/Controls/Dialogs/ConfigShare/*.xaml`) | Export and message layouts. The code-behind files process the actions and the state changes. |
| `ConfigShareImportDialog` (`CDPIUI/Windows/Messages`) | Opens when the user opens a `.cdpiconfig` file |
| The ConfigImport wizard | Shows its usual review, test and save steps for `.cdpiconfig` files |
| `WindowsPresetShare` (`CDPIUI/Helper/AddOns/ConfigShare`) | Windows sharing through [per-window desktop interop](https://learn.microsoft.com/windows/apps/develop/ui/display-ui-objects) |

The two import entry points offer the Store installer of the component when the required component is missing (`ConfigShareUI.OfferComponentInstallAsync`). Zapret Legacy is available when Zapret2 is installed. In this case, the wizard uses the existing conversion from Legacy to Zapret2.

## Tests

`CDPIUI.Tests/AddOns/ConfigShareWriteOptionsTests.cs` checks `EnsureNoOutsideWrites`. It makes sure that file writes outside the package are rejected and that writes inside the package and a plain `--debug` are accepted.

The solution has no separate test project for the round trip, the dependencies, the destinations, damaged archives or the cleanup.
