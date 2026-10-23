# GoodCheck add-on

## Purpose

GoodCheck is an external add-on. It is a separate executable file that the user installs from the Store. The Store ID is `ASGKOI001` (`HardcodedItemIds.AddOnsIds[AddOns.GoodCheck]`). GoodCheck starts a component with each strategy from a strategy list and checks the sites from a site list. It writes the results to a log file.

The folder `CDPIUI.AddOns/GoodCheck` contains the CDPI UI side of the add-on. This code prepares the site lists, sends the start request to the tray, reads the GoodCheck log, counts the results and writes an XML report. The CDPI UI code does not contain the GoodCheck algorithm.

## Supported components

GoodCheck supports only these components:

| Component | `HardcodedItemIds` key | GoodCheck name (`-f`) |
|---|---|---|
| ByeDPI | `Components.ByeDPI` | `ciadpi` |
| GoodbyeDPI | `Components.GoodbyeDPI` | `gdpi` |
| Zapret Legacy | `Components.Zapret` | `zapret` |

`GoodCheckResultViewHelper.ComponentNormalNames` contains this map. `GoodCheckProcessService.IsComponentSupported` uses it. `InitGoodCheck` throws `NotSupportedException` for other components. The GUI uses the same list from `HardcodedItemIds.GoodCheckSupportedComponents`.

For Zapret2, the auto-selection window opens BlockCheck2 instead. Refer to [BlockCheck2/README.md](../BlockCheck2/README.md).

## Main types

| Type | File | Function |
|---|---|---|
| `GoodCheckProcessService` | `GoodCheckProcessService.cs` | Singleton. Prepares the lists, starts the checks one by one, reads the logs, raises events and writes the report. |
| `GoodCheckProcessMode` | `GoodCheckProcessService.cs` | `AllAsOne` or `AnyAsAny` |
| `GoodCheckSiteListModel` | `GoodCheckProcessService.cs` | A site list and its strategy list |
| `GoodCheckOperationModel` | `GoodCheckProcessService.cs` | One check: ID, list name, output, state, error code and cancel token |
| `GoodCheckOperationType` | `GoodCheckProcessService.cs` | `Wait`, `WorkInProgress`, `SuccessFinish`, `ErrorHappens` or `UserInterrupt` |
| `GoodCheckResultViewHelper` | `GoodCheckResultViewHelper.cs` | The component names, the available strategy lists and the report loader |
| `StrategyModel`, `GroupModel`, `GoodCheckStrategiesList` | `GoodCheckResultViewHelper.cs` | Report and strategy list models |
| `SiteListHelper` | `SiteListHelper.cs` | Finds the site lists of the installed config kits |
| `DomainValidationHelper` | `DomainValidationHelper.cs` | Checks that a TXT file is a domain list |

## Modes

| Mode | Function |
|---|---|
| `AllAsOne` | The service merges all selected site lists into one list. It checks this list with one strategy list. |
| `AnyAsAny` | The service checks each site list separately, each with its own strategy list. |

In `AllAsOne` mode, the service uses the strategy list of the first site list. It writes the merged list as `<Unix time>_sitelist.txt`. The merge removes empty lines and duplicate lines and keeps the order. The merge writes UTF-8 without BOM, because GoodCheck reads a BOM as a part of the first domain.

In `AnyAsAny` mode, the service copies each site list into the `CheckLists` folder. If two lists have the same file name, the second copy gets the prefix `<index>_`.

The GUI disables the mode switch for GoodbyeDPI. Thus GoodbyeDPI uses only `AllAsOne`.

## Files of the add-on

All paths are relative to the add-on folder. `GoodCheckProcessService.GetAddOnDirectory()` returns the folder from the Store DB. If the DB has no valid folder, it returns `Store/Items/ASGKOI001`.

| Path | Contents |
|---|---|
| `<Executable>.exe` | The GoodCheck executable. The name comes from the `Executable` field of the Store item. |
| `config.ini` | The GoodCheck settings |
| `CheckLists/` | The site lists of the current run |
| `StrategiesGoGo/<ciadpi|gdpi|zapret>/*.txt` | The strategy lists of each component |
| `Logs/logfile_GoodCheckGoGo_*.log` | The logs that GoodCheck writes |
| `Reports/GoodCheckReport_<Unix time>.xml` | The reports that CDPI UI writes |

Before each run, the service deletes merged lists (`<digits>_sitelist.txt`) that are older than one day.

## Sequence of a run

1. The user opens the auto-selection window and selects a supported component.
2. If GoodCheck is not installed, the window opens the Store download dialog for `ASGKOI001`.
3. On the `CreateViaGoodCheck` page, the user selects the site lists, the mode and the strategy lists.
4. The user sets the additional settings. The page saves them (refer to "Settings").
5. The page calls `InitGoodCheck` with the component ID, the mode and the list models. The service prepares the lists and makes one `GoodCheckOperationModel` for each check.
6. The page opens `GoodCheckWorkPage` and calls `Start`.
7. For each check, the service sends the pipe message `GoodCheckMessageIds.Start` with the executable path and the arguments.
8. The tray starts GoodCheck. Refer to "Tray process".
9. The service reads the newest GoodCheck log and updates the progress.
10. After the last check, or after a cancel, the service writes the XML report and raises `AllComplete`.
11. `GoodCheckWorkPage` shows the end state. The forward button opens the report in `CreateConfigHelperWindow`.

The service uses these arguments:

```text
-q -p <passes> -f <ciadpi|gdpi|zapret> -m <curl|native> -c "<site list file name>" -s "<strategy list file name>"
```

The service passes only the file names of the site list and the strategy list.

## Tray process

`CDPIUI.TrayIcon/Helper/GoodCheckProcessHelper.cs` starts the process. It operates with administrator rights. It does these steps:

1. It makes sure that the executable is in a trusted path (`TrustedPaths.IsAllowed`). Otherwise, it reports `ERR_UNABLE_START_PROCESS`.
2. It stops all component tasks.
3. It sends `Running` to the GUI and blocks the component buttons.
4. It starts GoodCheck without a window. The working folder is the folder of the executable.
5. It puts the process into a job object that kills the process when the job closes.
6. When the process stops, it sends `Died` with the operation ID.
7. It unblocks the component buttons.

A start error gives `DiedViaError` with `ERR_UNABLE_START_PROCESS` or `ERR_WIN32_EXCEPTION`. The message `Stop` kills the process. When the pipe disconnects, the tray also stops GoodCheck.

In the GUI, `CDPIUI/Commands/CommandsHandler.cs` sends `Died` to `OperationWithIdDied` and `DiedViaError` to `OperationWithIdFailed`. `OperationWithIdFailed` marks the operation as failed and stops the log monitor of the operation.

## Log monitor

`MonitorProcessAndLogsAsync` reads only the logs that GoodCheck made after the start of the current check (2 seconds earlier at most). It selects the newest file, reads it from the start and reads it again every 500 ms.

For each line, the service does these checks:

| Text in the line | Result |
|---|---|
| `Launching '<name>', strategy N/M: [<strategy>]` | Raises `ProgressChanged` |
| `This strategy has no successes` | Increases the incorrect count |
| `worst result for this strategy: S/A` followed by `Terminating program...` | Raises `StrategyResultAdded`. If `S * 100 / A >= 65`, the correct count increases. Otherwise, if `S` is not 0, the incorrect count increases. |
| `Exiting with an error...` | The check fails with `ERR_GOODCHECK_EXCEPTION` |
| `All Done` | The check is successful |

When the tray reports `Died`, the service reads the rest of the log. If the rest does not contain `All Done` or the error text, the check fails with `ERR_GOODCHECK_EXITED_EARLY`.

The service keeps the full output of each check in `GoodCheckOperationModel`. It also raises `OperationOutputAdded` for each line.

## Report

`CreateAndSaveReport` writes the XML report:

```xml
<Report>
  <Group Name="<list name>" FullPath="<list path>" ComponentId="<component ID>">
    <StrategyResult>
      <Flag>false</Flag>
      <Strategy>...</Strategy>
      <Success>S</Success>
      <All>A</All>
    </StrategyResult>
  </Group>
</Report>
```

The service makes one `Group` for each operation. It writes the report also after a cancel. `GoodCheckResultViewHelper.LoadGroupsFromFile` reads the report. A group without `ComponentId` makes the file invalid.

The user can open reports in these places:

- `GoodCheckWorkPage`, after the run;
- `RecentGoodCheckSelectionsContentDialog`, which lists the `Reports` folder;
- the "open report" button of `CreateConfigHelperWindow`, with a file dialog.

`ViewGoodCheckReportPage` and `ViewGoodCheckSiteListReportPage` (`CDPIUI/Views/CreateConfigHelper`) show the report.

## Cancel

| Action | Method | Result |
|---|---|---|
| Stop all checks | `Stop` | Cancels the run token and sends `Stop` to the tray |
| Stop the current check | `RemoveFromQueueOrStopOperation(CurrentOperationId)` | Sets the check to `UserInterrupt` and cancels its token |
| End the service state after the report | `Kill` | Cancels and disposes the run token |

When the run ends, the service sets each check that is still `WorkInProgress` to `UserInterrupt`. If the user closes `CreateConfigUtilWindow` during a run, the window asks to stop the run and calls `Stop`.

## Errors

| Code | Source | Condition |
|---|---|---|
| `ERR_ADDON_NOT_INSTALLED` | Service | GoodCheck is not installed, or its executable is missing |
| `ERR_EXE_NOT_FOUND` | Service | The executable is missing when a check starts |
| `ERR_GOODCHECK_EXCEPTION` | Service | The log contains `Exiting with an error...` |
| `ERR_GOODCHECK_EXITED_EARLY` | Service | The process stopped before `All Done` |
| `ERR_MONITOR_EXCEPTION` | Service | The log monitor failed |
| `ERR_INTERNAL` | Service | An unhandled exception in the run |
| `ERR_UNABLE_START_PROCESS` | Tray | The path is not trusted, or the process did not start |
| `ERR_WIN32_EXCEPTION` | Tray | The job object or the process start failed |

## Settings

The page `CreateViaGoodCheck` (`CDPIUI/Views/CreateConfigUtil/CreateViaGoodCheckPage.xaml.cs`) reads and writes the settings.

CDPI UI settings, group `ADDONS/ASGKOI001`:

| Key | Type | Function |
|---|---|---|
| `passesValue` | `string` | The number of passes (`-p`). A value that is not an integer of 1 or more becomes 1. |
| `useCurl` | `bool` | `true` gives `-m curl`, `false` gives `-m native` |

For ByeDPI, the page offers only the curl resolver.

GoodCheck `config.ini` (UTF-8):

| Section | Keys |
|---|---|
| `General` | `ConnectionTimeout`, `AutomaticGoogleCacheTest` |
| `Advanced` | `ResolverNativeTimeout`, `ResolverNativeRetries`, `InternalTimeoutMs`, `AutomaticConnectivityTest`, `ConnectivityTestURL`, `SkipCertVerify`, `GoogleCacheMappingURLs` |
| `Resolvers` | `UseDoH`, `DoHResolvers` |
| `Fakes` | `FakeSNI`, `FakeHexStreamTCP`, `FakeHexStreamUDP`, `FakeHexBytesTCP`, `FakeHexBytesUDP`, `PayloadTCP`, `PayloadUDP` |
| `Zapret`, `GoodbyeDPI`, `ByeDPI` | The folder and the executable name of each installed component. The folder comes from the Store database. |

If `config.ini` is missing, the page tells the user to install the add-on again.

## Site lists

`SiteListHelper.GetAllAvailableSiteListTemplatesAsync` finds the TXT files in the folders of all `configlist` Store items. If one kit has several files with the same name and different contents, all of them stay in the list. Files with the same name and the same SHA-256 appear once.

`DomainValidationHelper` checks a list. In `Quick` mode, it reads the first 10 lines that are not empty and not comments. A list is not valid if a line contains whitespace or `/`, or if a line is an IP address. A file that the user selects must pass the `Quick` check. The page then copies it to `Store/Items/LocalUserStorage/List` with a unique name.

## GUI entry points

| Entry point | Location |
|---|---|
| Auto-selection window, component selection | `CDPIUI/Views/CreateConfigUtil/MainPage.xaml.cs` (`BeginNewSelectionButton_Click`) |
| Route | `cdpiui://Tools/AutoConfig/{componentId}` |
| Setup pages | `CDPIUI/Views/CreateConfigUtil/CreateViaGoodCheckPage.xaml.cs` (class `CreateViaGoodCheck`) |
| Progress page | `CDPIUI/Views/CreateConfigUtil/GoodCheckWorkPage.xaml.cs` |
| Site list controls | `CDPIUI/Controls/GoodCheck` |
| Report pages | `CDPIUI/Views/CreateConfigHelper/ViewGoodCheckReportPage.xaml.cs`, `ViewGoodCheckSiteListReportPage.xaml.cs` |
| Recent reports | `CDPIUI/Controls/Dialogs/CreateConfigHelper/RecentGoodCheckSelectionsContentDialog.xaml.cs` |

## Limits

- Only one GoodCheck run can operate, because `GoodCheckProcessService` is a singleton and the tray has one process slot.
- The tray stops all components before GoodCheck starts.
- The service passes only file names with `-c` and `-s`. GoodCheck must find the files from its own folder. For this reason, the page shows only the strategy lists in the top level of `StrategiesGoGo/<component>`, not in its subfolders.
- In `AllAsOne` mode, only the strategy list of the first site list is used.
- The correct and incorrect counters use a fixed threshold of 65%.
- The add-on has no tests in `CDPIUI.Tests`.
