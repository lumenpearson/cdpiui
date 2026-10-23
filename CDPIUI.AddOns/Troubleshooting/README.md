**English** | [Русский](README.ru.md)

# Troubleshooting add-on

## Purpose

The Troubleshooting add-on finds common causes of problems on the computer and in the Store. It can fix some of them. `TroubleshootingService` (`TroubleshootingService.cs`) contains all the logic. The add-on has two checks:

- the basic check: Windows services, network settings and programs that can stop a DPI bypass;
- the Store check: the access to the Store repositories and the write access to the Store folders.

## Main types

| Type | Function |
|---|---|
| `TroubleshootingService` | Singleton (`Instance`). Runs the checks and the fixes. |
| `RunBasicDialogStates` | The steps of the basic check |
| `StoreCheckStates` | The steps of the Store check |
| `DiagnosticStateModel` | The correct value of a step and the keys of its display texts |
| `FixResultModel` | The result of a fix: `IsFixed` and `ErrorCode` |
| `BasicDiagnosticStateModelPairs`, `StoreDiagnosticStateModelPairs` | Static maps from a step to its `DiagnosticStateModel` |

The service raises `CurrentStateChanged` with the current step before each check.

## Basic check

`RunBasicDiagnostic` first stops all component tasks. Then it sends the `StopService` request to the tray (`ProcessService.StopService`) and does not wait for the result. The tray stops the GoodbyeDPI, Zapret and Zapret2 tasks and runs `sc stop WinDivert` and `sc delete WinDivert`. The service then waits 1 second and does the checks below. It returns a map from each step to a `bool` value.

| Step | Check | Correct value |
|---|---|---|
| `CheckBFE` | The service `BFE` operates | `true` |
| `CheckProxy` | The Windows proxy is enabled (`WindowsNativeProxySettingsService.IsProxyEnabled`) | `false` |
| `CheckNetsh` | `netsh.exe` is in the current folder or in `PATH` | `true` |
| `CheckTimestamps` | PowerShell `(Get-NetTCPSetting -SettingName Internet).Timestamps` returns `Enabled` | `true` |
| `CheckAdGuard` | A process `adguardvpnsvc`, `adguardvpn` or `adguardsvc` operates | `false` |
| `CheckKiller` | A service with `Killer` in its name operates | `false` |
| `CheckIntelConnectivityNetwork` | A service with `Intel` and `Connectivity Network` in its name or display name operates | `false` |
| `CheckCheckPointServices` | A service with `TracSrvWrapper` or `EPWD` in its name operates | `false` |
| `CheckSmartByte` | A service with `SmartByte` in its name operates | `false` |
| `CheckVPNs` | A service with `vpn` in its name operates | `false` |
| `CheckDNS` | At least one network interface has DoH flags (`DohFlags > 0` under `HKLM:System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters`) | `true` |
| `CheckWinDivert` | A service with `windivert` in its name operates | `false` |
| `CheckAnotherComponentsServices` | A service with `GoodbyeDPI`, `discordfix_zapret`, `winws1` or `winws2` in its name exists | `false` |
| `CheckAnotherComponents` | A process `winws`, `winws2` or `goodbyedpi` operates | `false` |

The service name checks ignore the case. A service is "operating" if its state is `Running`, `StartPending` or `StopPending`. The service reads the service list one time for each check run.

The PowerShell calls use `powershell.exe -NoProfile -NonInteractive -Command`. The service stops a tool after 30 seconds.

## Store check

`RunStoreDiagnostic` also stops all component tasks and sends the `StopService` request first. It then does these checks:

| Step | Check | Correct value |
|---|---|---|
| `CheckGitHubRepo` | `StoreHelper.TryLoadDatabaseForVersionControl(GitHub)` | `true` |
| `CheckGitLabRepo` | `StoreHelper.TryLoadDatabaseForVersionControl(GitLab)` | `true` |
| `CheckWriteAccess` | Write access to `Directories.StoreDirectory` | `true` |
| `CheckWriteAccessIntoItemsFolder` | Write access to `Directories.StoreItemsDirectory` | `true` |
| `CheckWriteAccessIntoRepoFolder` | Write access to `Directories.StoreRepoCacheDirectory` | `true` |
| `CheckWriteAccessIntoDatabaseFolder` | Write access to `Directories.StoreLocalCacheDirectory` | `true` |

The write check makes a temporary file with a random name and the `DeleteOnClose` option.

## Fixes

`FixAllBasicErrors` and `FixAllStoreErrors` try to fix each step whose value is different from the correct value. All other steps return `IsFixed = false` and `INFO_CANNOT_FIX`.

| Step | Fix |
|---|---|
| `CheckTimestamps` | Runs `netsh interface tcp set global timestamps=enabled`. The fix is successful if the exit code is 0. |
| `CheckWinDivert` | Sends the `StopService` request through `ProcessService.StopService`. It then checks every 500 ms, a maximum of 20 times, until the service does not operate. |
| `CheckWriteAccess`, `CheckWriteAccessIntoItemsFolder`, `CheckWriteAccessIntoRepoFolder`, `CheckWriteAccessIntoDatabaseFolder` | Sends a `GrantAccessRequest` pipe message for the folder to the tray (`PipeHelper.SendGrantAccessPacket`) |

A failed fix returns `ERR_WIN32_UNKNOWN`.

The tray changes the access only for paths in the Store (`TrustedPaths.IsInStore`). It sends the answer `GrantAccessResponse`. `CDPIUI/Commands/CommandsHandler.cs` gives the answer to `TroubleshootingService.OnGrantAccessCompleted`. If no answer comes in 30 seconds, the fix returns `false`.

## GUI entry points

| Entry point | Location |
|---|---|
| Window | `CDPIUI/Windows/TroubleshootingWindow.xaml(.cs)` |
| Start page with the two checks | `CDPIUI/Views/Troubleshooting/MainPage.xaml.cs` |
| Progress, results and fixes | `CDPIUI/Views/Troubleshooting/WorkPage.xaml.cs` |
| Tools page | `CDPIUI/Views/UtilsPage.xaml.cs` |
| Component error window | `CDPIUI/Windows/Messages/ComponentErrorWindow.xaml` (link `cdpiui://Tools/Troubleshooting`) |
| Store download error dialog | `CDPIUI/Controls/Dialogs/Store/DownloadErrorActionsContentDialog.xaml.cs` |

The routes in `CDPIUI/Commands/CommandUriMapper.cs` are:

```text
cdpiui://Tools/Troubleshooting
cdpiui://Tools/Troubleshooting/BasicCheck
cdpiui://Tools/Troubleshooting/StoreCheck
```

`BasicCheck` opens `WorkPage` with the action `BeginBasicCheck`. `StoreCheck` opens it with `BeginStoreRepoCheck`. `WorkPage` shows each result with the display text keys from `DiagnosticStateModel`.

## Limits

- The two checks stop all components. The components do not start again automatically after the check.
- Most name checks use a substring of the service name. For example, `CheckVPNs` finds any operating service with `vpn` in its name.
- `CheckAnotherComponentsServices` checks that a service exists, not that it operates.
- Only the timestamps, WinDivert and Store folder access steps have a fix.
- On an operating system other than Windows, the service checks return `true`.
- The add-on has no settings and no tests in `CDPIUI.Tests`.
