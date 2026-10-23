# NewSetup

WiX Toolset 6 project (`WixToolset.Sdk/6.0.2`) that builds the x64 per-machine MSI published as
`Setup.msi`.

## Files

| File | Content |
|------|---------|
| `NewSetup.wixproj` | SDK project, x64 only, WiX extensions (UI, NetFx, Iis), dialog bitmaps and license. ICE64 is suppressed. |
| `Product.wxs` | Package, upgrade rule, install folder and its permissions, custom actions. |
| `NewSetupFiles.wxs` | `ComponentGroup NewSetupFiles`: one component per installed file, with the shortcuts. |
| `UI.wxs` | `WixUI_FeatureTree` dialog set. |
| `Resources/` | `Banner.jpg`, `Dialog.jpg`, `EULA.rtf` (license page), `Icon.ico` (Add/Remove Programs icon). |

## Source folder

`NewSetupFiles.wxs` defines

```xml
<?define SourceDir="..\CDPIUI\bin\x64\Release\net8.0-windows10.0.22621.0\publish"?>
```

This is the folder produced by `.github/scripts/build_portable.ps1` (GUI publish output plus
`CDPIUI_TrayIcon.exe`, `Update.exe` and `EditHostFile.exe`). The path is fixed to the Release
configuration, so run the script with its default configuration before building the MSI.

## File list rule

Every file of the publish folder except `*.pdb` must have its own component in
`NewSetupFiles.wxs`, with `Source="$(var.SourceDir)\<relative path>"`. WiX fails on a listed file
that does not exist, but a published file that is not listed would silently be missing from the
MSI. `.github/scripts/check_setup_files.ps1` compares both lists (case-insensitive) and fails
on any difference in either direction; CI and the release workflow run it before the MSI build.

When the publish output changes (new dependency, asset, help page, removed file), update
`NewSetupFiles.wxs` and run the check again. Two files have fixed IDs that `Product.wxs` uses:
`CdpiUiExe` (`CDPIUI.exe`, also carries the Start menu and desktop shortcuts) and `TrayIconExe`
(`CDPIUI_TrayIcon.exe`).

## Package and upgrades

- `Version` is bound to the file version of `CDPIUI.exe` (`!(bind.FileVersion.CdpiUiExe)`).
  The `MSIProductVersion` property in `NewSetup.wixproj` is defined but not used by the package.
- `UpgradeCode` is `{AB14590E-0D10-416F-9A0E-07E63074B603}`. Each build gets a new product code,
  so every install is a major upgrade of the previous one.
- `MajorUpgrade` blocks downgrades ("A newer version of [ProductName] is already installed.")
  and sets `AllowSameVersionUpgrades="yes"`: Windows Installer ignores the fourth version field,
  so builds that differ only in it still replace each other.
- The cabinet is embedded in the MSI. `MSIUSEREALADMINDETECTION` is `1`.

## Install folder and permissions

Default location: `ProgramFiles6432Folder\Storik4\CDPI UI`. The folder can be changed in the
feature tree (`ConfigurableDirectory="INSTALLLOCATION"`).

The elevated tray runs files from this folder, so the `InstallLocationPermissions` component sets
an explicit, protected ACL on it:

```
D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;BU)
```

Full control for SYSTEM and Administrators, read and execute for Users, no inherited entries.
A custom location such as `C:\CDPI UI` therefore does not inherit write access for all users
from its parent.

## Custom actions

| Action | When | What |
|--------|------|------|
| `LaunchApp` | After `InstallFinalize`, condition `NOT Installed AND NOT REMOVE AND UILevel >= 4` | Runs `[WindowsFolder]explorer.exe "[INSTALLLOCATION]CDPIUI.exe"`, `asyncNoWait`. Through Explorer the GUI runs as the signed-in user, not with the installer's rights. `UILevel >= 4` limits it to reduced and full UI, so silent (`/qn`) and basic-UI (`/qb`) installations do not start the GUI. |
| `UninstallCleanup` | Before `RemoveFiles`, condition `REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE` | Deferred, not impersonated (SYSTEM): `CDPIUI_TrayIcon.exe --uninstall-cleanup`. `Return="ignore"`. Not run when the product is removed by a major upgrade. |

`--uninstall-cleanup` (`CDPIUI.TrayIcon/Helper/UninstallHelper.cs`) sets
`Global\CDPIUI_TrayShutdown` and waits up to 20 s for running trays to exit (they stop the
components and restore the proxy settings), kills remaining processes started from the
installation folder, stops and deletes `WinDivert*` services whose image is in the installation
folder, deletes the autorun task, runs `EditHostFile.exe /remove`, and deletes runtime files the
MSI does not know: `Logs`, `.update-staging`, `.update-backup`, `.old`, `.staging`,
`update.log`, `.update-manifest`.

## Build

```powershell
pwsh .github/scripts/build_portable.ps1
pwsh .github/scripts/check_setup_files.ps1
dotnet build NewSetup/NewSetup.wixproj -c Release -p:Platform=x64
```

The MSI is written under `NewSetup/bin/` with the output name `NewSetup`. The release workflow
copies it as `Setup.msi`. The MSI is not Authenticode-signed (`CDPIUI.TrayIcon/Helper/Utils.cs`).
