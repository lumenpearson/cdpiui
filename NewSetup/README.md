**English** | [Русский](README.ru.md)

# NewSetup

NewSetup is a WiX Toolset 6 project (`WixToolset.Sdk/6.0.2`). It builds the x64 per-machine MSI.
The release contains this MSI as `Setup.msi`.

## Files

| File | Content |
|------|---------|
| `NewSetup.wixproj` | SDK project for x64 only. Contains the WiX UI extension (`WixToolset.UI.wixext`), the dialog bitmaps and the license. Suppresses ICE64. |
| `Product.wxs` | The package, the upgrade rule, the installation folder, its permissions, the `InstallLocation` registry value and the custom actions. |
| `NewSetupFiles.wxs` | `ComponentGroup NewSetupFiles`: one component for each installed file. Also contains the shortcuts. |
| `UI.wxs` | The `WixUI_FeatureTree` dialog set. |
| `Resources/` | `Banner.jpg`, `Dialog.jpg`, `EULA.rtf` (license page), `Icon.ico` (icon in "Add or remove programs"). |

## Source folder

`NewSetupFiles.wxs` defines the source folder:

```xml
<?define SourceDir="..\CDPIUI\bin\x64\Release\net8.0-windows10.0.22621.0\publish"?>
```

`.github/scripts/build_portable.ps1` makes this folder. It contains the GUI publish output,
`CDPIUI_TrayIcon.exe`, `Update.exe`, `EditHostFile.exe` and `CDPIUI_ElevatedLauncher.exe`. The first
three tools are .NET 8 single-file executables. `CDPIUI_ElevatedLauncher.exe` is a Native AOT
executable. The tools have no `.config` or `.dll` files of their own in the folder.

CAUTION: Run `build_portable.ps1` with the default `Release` configuration before you build the MSI.
The source path contains `Release`.

## File list rule

Each file in the publish folder must have its own component in `NewSetupFiles.wxs`.
Files with the `.pdb` extension are the only exception. The component must use
`Source="$(var.SourceDir)\<relative path>"`.

WiX fails if a listed file does not exist. But WiX does not find a published file that is not
listed. The MSI then does not contain this file, and WiX shows no error.

`.github/scripts/check_setup_files.ps1` compares the two lists without case sensitivity. It fails
if a file is missing from one of the lists. CI and the release workflow run it before the MSI build.

When the publish output changes, do these steps:

1. Run `build_portable.ps1`.
2. Add or remove the related components in `NewSetupFiles.wxs`.
3. Run `check_setup_files.ps1`.

`Product.wxs` uses two fixed file IDs:

| ID | File | Use |
|----|------|-----|
| `CdpiUiExe` | `CDPIUI.exe` | Package version. Start menu and desktop shortcuts. |
| `TrayIconExe` | `CDPIUI_TrayIcon.exe` | `UninstallCleanup` custom action. |

## Package and upgrades

- The package `Version` is the file version of `CDPIUI.exe` (`!(bind.FileVersion.CdpiUiExe)`).
- `NewSetup.wixproj` defines no version property. Only the file version of `CDPIUI.exe` sets the package version.
- The `UpgradeCode` is `{AB14590E-0D10-416F-9A0E-07E63074B603}`.
- Each build gets a new product code. Thus each installation is a major upgrade of the previous one.
- `MajorUpgrade` prevents downgrades with the message "A newer version of [ProductName] is already installed."
- `MajorUpgrade` sets `AllowSameVersionUpgrades="yes"`. Windows Installer ignores the fourth version field. With this setting, two builds that differ only in that field can replace each other.
- The MSI contains the cabinet file. `MSIUSEREALADMINDETECTION` is `1`.

## Installation folder and permissions

The default folder is `ProgramFiles6432Folder\Storik4\CDPI UI`. The user can change the folder in
the feature tree (`ConfigurableDirectory="INSTALLLOCATION"`).

The elevated tray runs files from this folder. Thus the `InstallLocationPermissions` component sets
an explicit, protected access control list on the folder:

```
D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;BU)
```

| Account | Rights |
|---------|--------|
| SYSTEM | Full control |
| Administrators | Full control |
| Users | Read and execute |

The folder does not inherit entries from its parent. Thus a custom folder such as `C:\CDPI UI`
does not give write access to all users.

## Installation location in the registry

The `InstallLocationRegistration` component writes the installation folder to the registry:

| Key | Value name | Data |
|-----|------------|------|
| `HKLM\Software\Storik4\CDPI UI` | `InstallLocation` | `[INSTALLLOCATION]` |

The release workflow packs the same `Release` build as the MSI and as the portable ZIP. The GUI
(`CDPIUI.Core/State.cs`) and the tray (`CDPIUI.TrayIcon/Helper/Utils.cs`) compare this value with
their own folder. If the two folders are equal, the application operates as an MSI installation.

CAUTION: Do not remove the `InstallLocationRegistration` component. Without it, an MSI installation
operates as a portable installation and downloads patch updates.

## Custom actions

| Action | Sequence and condition | Function |
|--------|------------------------|----------|
| `LaunchApp` | After `InstallFinalize`. Condition: `NOT Installed AND NOT REMOVE AND UILevel >= 4`. | Runs `[WindowsFolder]explorer.exe "[INSTALLLOCATION]CDPIUI.exe"` with `asyncNoWait`. |
| `UninstallCleanup` | Before `RemoveFiles`. Condition: `REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE`. | Runs `CDPIUI_TrayIcon.exe --uninstall-cleanup`. Deferred, not impersonated (runs as SYSTEM), `Return="ignore"`. |

Notes on `LaunchApp`:

- Explorer starts the GUI. Thus the GUI runs as the signed-in user, not with the rights of the installer.
- `UILevel >= 4` means reduced UI or full UI. A silent (`/qn`) or basic UI (`/qb`) installation does not start the GUI.

Notes on `UninstallCleanup`:

- The action does not run when a major upgrade removes the old product.
- `--uninstall-cleanup` (`CDPIUI.TrayIcon/Helper/UninstallHelper.cs`) does these steps:

1. Set the `Global\CDPIUI_TrayShutdown` event.
2. Wait a maximum of 20 s for the running trays to exit. The trays stop the components and restore the proxy settings.
3. Kill the remaining processes that run from the installation folder or from a user folder `\AppData\Local\CDPIUI\` (`ApplicationPathMatcher`).
4. Stop and delete each `WinDivert*` service whose image is in one of these folders.
5. Delete the autorun task.
6. Run `EditHostFile.exe /remove`.
7. Delete the runtime files that the MSI does not know: `Logs`, `.update-staging`, `.update-backup`, `.old`, `.staging`, `.trust`, `.session`, `update.log`, `.update-manifest`.

## Build

Run these commands from the repository root:

```powershell
pwsh .github/scripts/build_portable.ps1
pwsh .github/scripts/check_setup_files.ps1
dotnet build NewSetup/NewSetup.wixproj -c Release -p:Platform=x64
```

The MSI goes into `NewSetup/bin/`. Its output name is `NewSetup`. The release workflow copies it
as `Setup.msi` and signs it with the catalog certificate. The tray installs an MSI update only
with this signature (`PackageStaging.HasPinnedSignature`). A local build is not signed.
