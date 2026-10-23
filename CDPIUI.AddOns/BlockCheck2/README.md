**English** | [Русский](README.ru.md)

# BlockCheck2 for Zapret2

## 1. Purpose and scope

BlockCheck2 is an internal add-on of CDPI UI. It finds Zapret2 strategies, combines them into one preset, checks the preset and lets the user edit it. The add-on does more than find single strategies that operate. After the scan, it makes one multi-profile preset, checks the complete preset and tries to replace a strategy that causes a conflict.

The main scenario is for HTTP and HTTPS sites. The data model and the catalog support QUIC, but the standard UI hides the QUIC option. The add-on does not find strategies for random UDP traffic of applications, for example Discord voice. A usual `curl` HTTP request cannot check such traffic.

BlockCheck2 is not a separate Store package. The algorithm is in the `CDPIUI.AddOns` project. The UI and the CDPI UI adapters are in the `CDPIUI` project.

## 2. Architecture

### 2.1. Core without UI

The folder `CDPIUI.AddOns/BlockCheck2` has these areas:

| Area | Contents |
|---|---|
| `Catalog` | The built-in strategy catalog, its loader, the expander and the validator |
| `Models` | Targets, strategies, probe results, profiles, scan options and validation options |
| `Execution` | The baseline check, temporary Zapret2 runs, `curl` requests, the test of one strategy and the check of a ready preset |
| `Analysis` | The normalization of equivalent strategies, the Pareto filter, the target groups and the coverage optimizers |
| `Synthesis` | The profile selection, the circular fallback, the manual preset and the Zapret2 command line |
| `Reporting` | The report builder, the serializer, the import and the session restore from a report |
| `Presentation` | The run modes, the input preparation, the site lists and the time estimates |

`BlockCheckAddOnService` is the entry point. It does not use the UI. It accepts the catalog, the targets, an `IBlockCheckStrategyRunner`, an `IBlockCheckProbeRunner`, the run options, a progress object and a `CancellationToken`.

### 2.2. CDPI UI integration

| Type | Location | Function |
|---|---|---|
| `BlockCheck2ViewModel` | `CDPIUI/ViewModels` | Wizard state, settings, session start, ETA, intermediate counters and report save |
| `Views/BlockCheck2/MainPage` | `CDPIUI/Views/BlockCheck2` | Pages for preparation, parameter review, progress and completion |
| `Views/BlockCheck2/ReportHistoryPage` | `CDPIUI/Views/BlockCheck2` | The list of recent selections |
| `BlockCheck2ResultViewModel` | `CDPIUI/ViewModels` | Report view, evidence table and state of the manual builder |
| `Views/BlockCheck2/ResultPage` | `CDPIUI/Views/BlockCheck2` | Selection result, manual preset and diagnostics |
| `Views/BlockCheck2/PresetReviewPage` | `CDPIUI/Views/BlockCheck2` | Review, expert edit, test and save of the ready preset |
| `BlockCheck2PresetEditorControl` | `CDPIUI/Controls/BlockCheck2` | Adapter between the BlockCheck2 draft and the common `ConfigMakerUserControl` |
| `BlockCheck2PresetStorageService` | `CDPIUI/Helper/AddOns/BlockCheck2` | Saves the preset in the user configs and copies the dependencies |
| `BlockCheckReportHistoryService` | `CDPIUI/Helper/AddOns/BlockCheck2` | Recent reports |
| `BlockCheck2HistoryStoreItemService` | `CDPIUI/Helper/AddOns/BlockCheck2` | Registers the history as a Store item that the user can remove |

## 3. Session sequence

1. The user selects one or more site lists, or makes a "custom site list".
2. For each list, the user selects one of two modes: the list as one unit, or each site separately.
3. The user selects the quick, medium or full mode.
4. On the page with more parameters, the user selects the protocols, the IP versions, the timeouts, the number of requests to each target and the option to test all strategies.
5. `BlockCheckSessionPreparationService` loads the built-in catalog, parses the input and makes one `BlockCheckTarget` for each combination of URL, protocol and IP version.
6. The parameter review page shows a conservative maximum duration.
7. `BlockCheckAddOnService` runs the preflight, the baseline, the strategy scan, the synthesis, the check of the ready preset and a possible repair.
8. `BlockCheckReportBuilder` makes a report. It also does this for a best-effort result and for a canceled session with partial data.
9. The view model saves the JSON report in the history automatically.
10. A separate result window lets the user accept the automatic preset or make a preset from all the evidence.

## 4. Input data and targets

### 4.1. Domains and URLs

`BlockCheckTargetInputParser` accepts:

- a DNS name without a scheme;
- an absolute `http://` or `https://` URL;
- a path and a query string;
- a non-standard port.

The parser ignores empty lines and lines that start with `#`. It converts domains to lowercase IDN ASCII. Only DNS names are valid. IP literals and other schemes cannot be targets.

A line without a scheme expands into all selected protocols. An explicit scheme limits the line to the compatible protocols. Each target also expands into all selected IP versions. The parser then removes duplicates.

One parser call makes a maximum of 200 targets (`BlockCheckTargetInputOptions.MaximumTargets`). If the input makes more targets, the parser returns `TARGET_INPUT_LIMIT_EXCEEDED`. The limit applies separately to the direct input and to each list in the `WholeList` mode. In the `EachSite` mode, the parser reads each domain separately.

An ID such as `target-005-tls12-ipv4` is internal. The UI shows the URL and the connection data separately, for example `https://example.org/path` and `TLS 1.2 · IPv4 · TCP:443`.

### 4.2. Site lists

These sources are available:

- lists that CDPI UI already knows;
- local TXT files from the file dialog;
- TXT files that the user drops on the page;
- a custom list that the user types in `CustomSiteListContentDialog`.

The loader removes a BOM and a leading `^` from each line. It ignores empty lines and `#` comments. The UI does not offer exclusion lists as usual input lists.

Each list has one of two modes:

- `WholeList`: the file stays one atomic unit. The final profile refers to the file with `--hostlist`.
- `EachSite`: the loader makes a separate temporary TXT file for each domain. Thus different sites of one list can get different strategies.

The temporary files have names such as `site_0001_<hash>.txt`. They are in `BlockCheck2/GeneratedSiteLists` in the CDPI UI data folder. The general list selection menu does not show them. If preparation or the run ends without a report that the user can open, the view model deletes the temporary files. If a result exists, the files stay, because the report or an unsaved preset can refer to them.

The custom list is in this folder:

```text
Store/Items/LocalUserStorage/List/BlockCheck2/Custom
```

### 4.3. Atomic lists

One `--hostlist` option covers the complete file. Thus the algorithm cannot put one list into several conflicting profiles and expect each profile to process only "its" domains.

`BlockCheckTargetGroupBuilder` puts these targets into one group:

- the checks of one runtime route;
- all targets of one list with the same connection shape;
- lists that share targets, transitively.

A strict strategy must operate for all targets of the group. The scan, the strict optimizer, the best-effort optimizer and `BlockCheckSynthesisService` use the same groups. Thus a site of a list that passes the baseline stays in its group, and the selected strategy must not break it.

If different domains of one atomic list pass only with different strategies, the synthesis adds the warning `NO_COMMON_SITE_LIST_STRATEGY`. If the synthesis then cannot make a profile, the issue with the same code is an error. To get different strategies for the sites, the user must select `EachSite`.

## 5. Protocols and runtime route

The model has these protocols:

- `Http`: TCP, port 80 by default;
- `Tls12`: HTTPS with forced TLS 1.2 and HTTP/1.1;
- `Tls13`: HTTPS with forced TLS 1.3 and HTTP/1.1;
- `TlsAuto`: HTTPS without a forced TLS version or ALPN;
- `Quic`: UDP and HTTP/3.

This tuple identifies a runtime route:

```text
host + IP version + transport + port + L7 protocol
```

TLS 1.2, TLS 1.3 and automatic TLS of one domain can give separate evidence rows. A usual Zapret2 profile cannot select an action by the TLS version after the ClientHello. Thus the strategy selection treats compatible TLS checks of one shape as one unit. A success on only one TLS version is partial.

The IP version is a part of the profile. The writer always adds `--filter-l3=ipv4` or `--filter-l3=ipv6`. If a browser can use the two IP versions, include the two versions in the selection. A success of `curl -4` does not prove that a browser that uses IPv6 can open the site.

## 6. Run modes

`BlockCheckRunPresetFactory` sets the scan, synthesis and preset check options together.

| Parameter | Quick | Medium | Full |
|---|---:|---:|---:|
| Recommended requests to each target | 2 | 3 | 5 |
| Maximum catalog tier | Quick | Balanced | Exhaustive |
| Success threshold | 100% | 80% | 80% |
| Different successful plans before early stop | 2 | 2 | 4 |
| Early stop of a route | Yes | Yes | No |
| Circular fallbacks for each profile | Up to 1 | Up to 1 | Up to 2 |
| Circular failure threshold (`fails`) | 2 | 3 | 3 |
| Maximum repair iterations | 1 | 3 | 5 |
| Optimizer preference | Speed | Balanced | Stability |

The mode sets the recommended number of requests. The user can then set an integer value from 1 to 20. The value applies to:

- the baseline checks;
- each strategy and target pair;
- the minimum number of evidence attempts in the synthesis;
- the check of each candidate of the ready preset;
- the maximum duration estimate.

The option "Continue after finding the best strategies" disables the early stop in the quick and medium tiers. The full mode tests the complete applicable catalog without this option. The option does not extend the selected tier. For example, the medium mode with this option does not test the Exhaustive strategies.

## 7. `BLOCKCHECK` settings

`SettingsManager` writes the settings to `Settings.xml`, group `BLOCKCHECK`.

| Key | Type | Default | Function |
|---|---|---:|---|
| `SelectedRunPreset` | `int` | `Balanced` | Selected mode |
| `IncludeHttp` | `bool` | `false` | Check HTTP |
| `IncludeTls12` | `bool` | `true` | Check TLS 1.2 |
| `IncludeTls13` | `bool` | `false` | Check TLS 1.3 |
| `IncludeTlsAuto` | `bool` | `true` | Check automatic HTTPS |
| `IncludeQuic` | `bool` | `false` | Check QUIC. The standard switch is hidden. |
| `IncludeIpv4` | `bool` | `true` | Check IPv4 |
| `IncludeIpv6` | `bool` | `false` | Check IPv6 |
| `ConnectTimeoutSeconds` | `double` | `5` | Connection timeout, 1–60 s |
| `RequestTimeoutSeconds` | `double` | `10` | Full request timeout, 2–180 s, not less than the connection timeout |
| `StartupGraceSeconds` | `double` | `0.75` | Pause after the Zapret2 start, 0.25–10 s |
| `TestAllStrategies` | `bool` | `false` | Disable the early stop |
| `RequestsPerDomain` | `int` | 2, 3 or 5 | Requests to each target, 1–20 |
| `historyStoreItemRegistrationCompleted` | `bool` | `false` | The one-time Store registration of the history is complete |

When the view model loads, it replaces an incorrect or out-of-range value with a safe default. A settings read or write error does not stop the selection. The view model writes the error to the CDPI UI log.

## 8. Strategy catalog

The seed catalog is the embedded resource `strategy-catalog.v1.json`. `BuiltInStrategyCatalogExpander` adds the standard families and sets the catalog version `2026.08.11-standard`.

The catalog has HTTP, TLS and QUIC variants of these families:

- `multisplit` and `multidisorder` with different positions;
- `fake` with `tcp_md5`, `badsum`, sequence, ack and timestamp changes, and TCP flags;
- `fakedsplit` and `fakeddisorder`;
- `hostfakesplit`;
- combinations of fake with split, faked or hostfake;
- QUIC fake and IP fragmentation.

Each `StrategyDefinition` sets:

- a stable `Id` and a display name;
- the family and the scan tier;
- the applicable protocols and IP versions;
- the base cost;
- circular support;
- the pre-host and inbound traffic requirements;
- the blobs;
- the ordered Lua actions, the payloads and the ranges.

`StrategyCatalogValidator` rejects:

- unknown Lua functions;
- unknown payload filters;
- duplicate IDs;
- incorrect ranges;
- the reserved arguments `strategy` and `final`;
- argument values with unsafe characters;
- blob sources with an absolute path or `..`.

The regular expressions for IDs, argument names, argument values and ranges end with `\z`, not with `$`. Thus a value with a final line break does not pass. The order of `--lua-desync` actions has a meaning, and the writer keeps it.

`CandidateNormalizer` makes a signature of the actual plan without the display data. If two catalog items have the same actions, the add-on counts them as one technical plan. This applies to the count of successful alternatives and to the fallback selection.

## 9. Environment preparation

`CdpiuiZapret2StrategyRunner` starts temporary arguments through the usual path: `ComponentTasksManager`, then the CDPI UI tray and ConPTY.

Before the baseline, the runner does these steps:

1. It makes sure that the Zapret2 component and its executable file are installed.
2. It gets an exclusive semaphore for the component.
3. It records which of these components operate: Zapret2, Zapret Legacy, GoodbyeDPI, ByeDPI, SpoofDPI and NoDPI.
4. It stops the components that cause a conflict.

After the scan or an error, the runner stops the temporary process. It then starts again the components that operated before. The cleanup uses `CancellationToken.None`. Thus a manual cancel does not stop the restore of the environment.

After each strategy start, the runner waits `StartupGraceSeconds`. If the process stops, the warning contains the last 800 characters of the process output. A start error of one strategy gives `SCAN_STRATEGY_FAILED`. This error does not delete the results of earlier strategies and does not stop the route. If 5 strategies in sequence do not start, the scan stops with the error `SCAN_STRATEGIES_DO_NOT_START`. Usually this means that winws2 or its driver cannot start.

## 10. Preflight and `curl`

`CurlBlockCheckProbeRunner` first finds out:

- if `curl.exe` is available;
- if curl supports `--tls-max`;
- if curl supports TLS 1.3;
- if curl supports HTTP/3.

If the curl build cannot do a selected check, the add-on returns a preflight error before it changes the state of the components. The errors are `CURL_NOT_AVAILABLE`, `CURL_TLS_MAX_UNSUPPORTED`, `CURL_TLS13_UNSUPPORTED` and `CURL_HTTP3_UNSUPPORTED`.

Each request uses `--connect-timeout`, `--max-time`, `--output NUL`, a machine-readable `--write-out`, a separate User-Agent and a fixed `-4` or `-6`. HTTP, TLS 1.2 and TLS 1.3 requests use `--http1.1`. TLS requests use a full GET by default, not HEAD. QUIC requests use HEAD and `--http3-only` by default. `TlsAuto` does not force HTTP/1.1 or a TLS version. Thus it is nearer to a usual HTTPS client, but it is not a browser.

An attempt is successful when `curl` exits with code 0 and returns an HTTP status that is not zero. For the `Http` protocol, the response also must not look like a provider stub. Status 400, status 451 and a redirect (3xx) to a different site are failures. Status 400 gives `http-400-possible-fake-leak`. The other stub responses give `http-<status>`.

The stable failure codes include:

- `curl-start-failed`;
- `proxy-resolution-failed`;
- `dns-resolution-failed`;
- `connection-failed`;
- `timeout`;
- `tls-handshake-failed`;
- `certificate-validation-failed`;
- `curl-feature-unsupported`;
- `http-status-missing`;
- `curl-exit-N`.

The report keeps the number of attempts, the number of successes, the success rate, the median TTFB, the p95 TTFB, the HTTP statuses and the failure codes.

## 11. Baseline

Before Zapret2 starts, the add-on checks each target. It uses the same number of requests, protocol, URL and IP version.

The baseline has three functions:

1. It prevents a bypass selection for a route that operates without Zapret2.
2. It gives a reference for a best-effort candidate.
3. It removes targets that the system DNS resolver cannot resolve.

If all attempts of a target end with `dns-resolution-failed`, the add-on adds the target to `IgnoredTargetIds`. The synthesis and the report do not use this target, and the issues get `DNS_TARGETS_IGNORED`. If no target resolves, the add-on returns `NO_RESOLVABLE_TARGETS`.

The add-on skips a route as accessible only when all checks of its atomic group reach the threshold of the mode. The message `NO_BYPASS_REQUIRED` applies only to the exact transport routes that the add-on checked. It does not prove that a browser shows the page, that HTTPS after an HTTP redirect is available or that the other IP version operates.

## 12. Strategy scan

The scan uses only strategies of the selected tier that apply to all targets of the atomic group. The order is progressive: low tiers come first. In a tier, the scan takes one strategy from each family in turn, and cheaper families come first.

One Zapret2 start serves one combination of a strategy and an atomic group. While the process operates, the scan checks all targets of the group with all repeats. This is important for TLS 1.2, TLS 1.3 and for lists: a strategy is successful for a group only if it reaches the threshold on each target.

The progress data contains:

- the phase;
- the completed and the total strategy jobs;
- the current URL;
- a separate connection description;
- the current strategy;
- the attempt number;
- the successful and the failed requests;
- the number of successful, technically different strategies.

With the early stop, a route stops after the scan finds the required number of different successful plans. With the full test, the scan tries all applicable strategies of the selected tier.

Errors of one request, of argument generation, of a process start or of a process stop become evidence and issues. The scan keeps the `ProbeResult` items that it has collected.

## 13. Automatic preset synthesis

### 13.1. Evidence

`ProbeResultAnalyzer` collects the attempts of each strategy and target into a `CandidateEvaluation`. A candidate passes the strict threshold only with enough attempts and the required success rate.

### 13.2. Pareto filter

`ParetoReducer` removes a candidate if another candidate:

- covers all targets that the first candidate covers (a candidate without strict coverage stays);
- has a minimum stability that is not lower;
- is not slower by the worst p95;
- does not have a higher base cost;
- is strictly better by at least one criterion.

Equivalent plans become one ID in a deterministic way.

### 13.3. Strict coverage

`CoverageOptimizer` solves a greedy coverage problem for the atomic groups. The score is the value of the new coverage divided by the effective cost. The effective cost uses:

- the base complexity of the strategy;
- the p95 latency;
- a penalty for instability;
- the weights of the Speed, Balanced or Stability mode.

If two scores are equal, the optimizer compares the number of routes, the minimum success rate, the p95 and the stable ID.

### 13.4. Best effort

If no strategy reaches the strict threshold, `BestEffortCoverageOptimizer` can select the "best of the bad" strategy. It does this only if the strategy gives a better result than the baseline. The priority is:

1. the number of targets that pass the strict threshold;
2. the number of targets with at least one success;
3. the total number of successful requests;
4. the general success rate;
5. the p95;
6. the base cost and the ID.

A best-effort result does not look like a validated result. The report and the profile get `IsBestEffort`, and the issues get `BEST_EFFORT_CONFIG`. If no strategy improves the baseline, the add-on does not make a preset and returns `NO_EFFECTIVE_STRATEGY`.

### 13.5. Profiles and fallback

`ProfilePlanner` merges assignments only if these items are the same:

- the normalized strategy plan;
- the IP version;
- the transport;
- the port;
- the L7 protocol;
- the best-effort flag.

Domains from the direct input go to `--hostlist-domains`. Targets from files keep their original paths and go to `--hostlist`. More specific domain profiles come before general profiles.

`CircularFallbackPlanner` adds only strategies that:

- are successful on all targets of the profile;
- support circular;
- do not require pre-host;
- have a different actual signature.

The primary strategy of the profile must also support circular and must not require pre-host. Several strategies that operate separately do not always operate together. Thus the add-on always checks the complete preset command line after the synthesis.

## 14. Zapret2 arguments

`Zapret2ConfigWriter` makes one command line.

Before the profiles, the writer adds the common WinDivert filters and libraries:

- `--wf-tcp-out` and `--wf-udp-out`;
- inbound ports only for a circular or inbound strategy;
- the common blobs;
- `--lua-init=@lua/zapret-lib.lua`;
- `--lua-init=@lua/zapret-antidpi.lua`;
- `--lua-init=@lua/zapret-auto.lua` if a profile uses circular.

`--new` separates the profiles. Each profile gets explicit values for:

- `--filter-l3`;
- `--filter-tcp` or `--filter-udp`;
- `--filter-l7`;
- `--hostlist` or `--hostlist-domains`;
- `--name`.

The writer always puts the path of each `--hostlist` in double quotes, also when the path has no spaces. It escapes quotes in the value.

For each Lua action, the writer adds the sticky filters `--payload`, `--in-range` and `--out-range` again. The line is longer, but no action can get the state of an earlier action or profile by accident.

A circular profile gets one common `circular` instance, numbered actions `strategy=N` and `final` on the last fallback strategy, if the writer options permit it.

## 15. Check and repair of the ready preset

`BlockCheckPresetValidationService` starts the complete synthesized command line and checks all active targets again.

If the candidate fails, the service does these steps:

1. It finds the profiles and the actual strategy signatures of the failed targets.
2. It removes these strategy and route pairs from the original evidence.
3. It runs the synthesis again.
4. It checks the new candidate completely.

The mode limits the number of repair iterations. The possible final issues are `PRESET_VALIDATION_FAILED`, `PRESET_REPAIR_LIMIT_REACHED`, `PRESET_REPAIR_STALLED` and `PRESET_REPAIR_NO_CANDIDATE`. A successful repair adds `PRESET_REPAIRED`.

An infrastructure failure does not prove that a strategy does not operate. The service records it separately.

## 16. Cancel and failure processing

A manual stop sets the session to `Canceling` and cancels the current request. The scan service then:

- stops the temporary Zapret2 process;
- keeps the completed baseline and strategy probes;
- adds `RUN_CANCELED` or `SCAN_CANCELED`;
- tries to run the synthesis with the available data;
- makes a partial report.

If the cancel occurs during the preset check, the report contains the scan results and the first synthesized candidate. The report does not contain the preset check attempts or the repair candidates. `WasCanceled` prevents a full success.

An unhandled exception outside the usual limits shows as an operation error. An error of the automatic history save does not delete the report in memory. The UI shows this error separately.

## 17. Time estimates

### 17.1. Before the run

`BlockCheckMaximumDurationEstimator` calculates a conservative limit. It assumes that 80% of the requests fail. A failed request takes the full `RequestTimeoutSeconds`.

The estimate includes:

- all baseline requests;
- the applicable strategy jobs;
- the number of repeats for each target;
- the startup grace of each process;
- the first preset check;
- the maximum number of repair checks.

With the early stop, the estimate assumes that one strategy of five is successful. Thus the estimate includes the number of jobs that it expects for the limit of successful strategies. With `TestAllStrategies`, the estimate includes the complete selected tier.

### 17.2. During the run

The ETA of the current stage uses an exponential moving average with `alpha = 0.2`. The UI also estimates the total remaining time. During the baseline, the add-on does not have enough data. Thus the UI shows "Calculating after the first completed strategies…" and not an invented number.

The counters of successes, errors, completed requests and found strategies change after each attempt.

## 18. Report

The JSON schema version is `2`. `BlockCheckReport` contains:

- the UTC time and the catalog version;
- the mode, `Success`, `WasCanceled` and `IsBestEffort`;
- the normalized targets and the runtime routes;
- the baseline, strategy and preset-validation probes;
- the final profiles and their scope;
- the primary and fallback IDs;
- the validation candidates;
- the issues;
- the ready line `PresetArguments`.

The report writes an undefined median or p95 as `null`. Thus the JSON does not contain `Infinity` or `NaN`.

These formats are available:

- JSON export for later work in the application;
- a text diagnostic report;
- a TXT file with the preset arguments.

The TXT export with arguments requires a successful or explicitly best-effort report with a line that is not empty.

## 19. Report import

`BlockCheckReportImportService` reads the file as strict UTF-8. It finds the format automatically: text that starts with `{` is JSON, other text is a text report. The service rejects an empty file and a file larger than 64 MiB.

The service checks the schema version (only `2`), the collections, the enum values, the IDs, the target references and the item counts:

| Item | Limit |
|---|---:|
| `Targets` | 20 000 |
| `Probes` | 200 000 |
| `Profiles` | 20 000 |
| `AttemptCount` of one probe record | 0–1 000 |

The service also checks `PresetArguments` with `ConfigShareService.EnsureNoOutsideWrites`. The tray runs the preset of a report with administrator rights. Thus the service rejects a report with options that write files: `--debug=@…`, `--wf-save`, `--hostlist-auto` and `--hostlist-auto-debug`. The synthesis never writes these options.

During the import, the service:

- loads the current catalog;
- restores the targets and the probes;
- connects the profiles to the strategies by ID. A profile with an unknown primary strategy is not restored;
- makes the attempts again from the stored counters. Each successful attempt gets the median time;
- makes a `BlockCheckResultSession` for manual work;
- adds warnings for missing list files and unknown strategies.

A different catalog version does not prevent the view. But the user cannot edit or test an unknown strategy.

A text report contains less structured data. Its parser finds the possible scope from the site lists that exist. It does not assign a missing list to all targets, because that would give a false precision.

## 20. Recent selections and the Store

The history is in this folder:

```text
Store/Items/BlockCheck2
```

The add-on does not migrate or read the old test folder `Reports/BlockCheck2`.

The file name contains the UTC time, the mode, the status and a short GUID. The history keeps a maximum of 50 JSON reports. After a successful write of a new report, the service deletes the older reports.

The Store item:

| Field | Value |
|---|---|
| `Id` | `BlockCheck2` |
| `Name` / `ShortName` | The localized string `RecentBlockCheck2Selections` |
| `Type` | `CDPIUIUpdateItem` |
| `VersionControlType` | `local` |
| `Developer` | `CDPIUI` |

`DatabaseInitializationService.QuickRestore` calls `RegisterOnFirstLaunch`. After the first successful registration, the service sets `BLOCKCHECK/historyStoreItemRegistrationCompleted=true`. Thus the next start does not add the item again after the user removes it in the Store memory settings.

Before the service saves a new report, `EnsureRegistered` makes the folder and the Store record if necessary. Thus a history that the user removed stays removed until BlockCheck2 makes new data.

The removal of the Store item uses the usual `StoreHelper.RemoveItem`. It deletes the report folder and the Store database record.

The page of recent selections lets the user:

- open a record in the result window;
- open a JSON or TXT report from a file;
- show the file in Explorer;
- delete one report after a confirmation;
- open the folder;
- refresh the list.

## 21. Integration into the auto-selection tools

BlockCheck2 is a part of `CDPIUI.CreateConfigUtilWindow`. It is available only when Zapret2 is installed.

The wizard uses `AnimatedHorizontalContentViewer`. The title and the description belong to `AnimatedHorizontalContentItem`. The page does not show them again.

The wizard pages are:

1. site lists and user input;
2. the selection mode;
3. more parameters;
4. the parameter review and the maximum duration;
5. progress;
6. completion.

The user can go back to the main menu from the wizard and from the page of recent selections. If the user closes the window during a run, the common stop dialog of the selection tools opens.

The public protocol routes are:

```text
cdpiui://Tools/AutoConfig/Zapret2
cdpiui://Tools/AutoConfig/Zapret2/Reports
```

The routes are in the explicit allow list of `CommandUriMapper`. They open `CreateConfigUtilWindow` with the necessary action.

## 22. Result window

`BlockCheck2ResultWindow` is a separate window with the Mica Alt backdrop. It has a common MenuBar, a status bar and a center for compact notifications. The rows of the window are:

```text
TitleBar
Function/Menu bar
Content Frame
Status bar
```

`ResultPage` has these sections in an `AnimatedSelectorBar`:

- "Selection result";
- "Preset builder";
- "Diagnostic information".

### 22.1. Selection result

The page shows the strategies by site. `SemanticZoom` switches between the full list and the group overview. The user can search, filter by successful, partial and failed results, and sort by result, site and response time.

A row shows the actual strategy name, the URL, the separate connection data, the number of successes, the success rate, the response time, the HTTP status, the failure code and a flag if the preset uses the strategy.

A warning symbol means that the strategy passed the current row, but the add-on did not prove it for the complete runtime area, for example for the other TLS version. A ToolTip explains the symbol.

The user can:

- test the strategy separately with a temporary profile and `curl`;
- start it as a temporary component for a manual check;
- add it for the current site;
- add it for the related site list;
- copy it as actions or as the full command line of the test profile.

### 22.2. Preset builder

The builder shows the automatic profiles as assignments. The user can remove, move or add assignments. The order of assignments in one scope sets the primary strategy and the circular fallbacks.

The manual builder checks:

- that the strategy and the target exist;
- that the strategy applies to the complete scope;
- circular support;
- that no actual plan is a duplicate;
- that evidence exists;
- that all targets of the scope pass the threshold;
- that the scopes do not overlap.

The builder shows identical warnings once, with a counter. It groups them by code, message and subjects. For `MANUAL_SCOPE_OVERLAP`, the user can quickly accept the warning in the builder. This does not hide other error types and does not change the assignment.

The preview of the command line uses `TextControlBox` in the common editor, with automatic format by flags. The button that opens the ready preset is in the builder area and in the action menu. Both open the same review page.

### 22.3. Diagnostics

The section shows the issues of the report and the full list of probes. The warnings of the manual builder are separate from the diagnostics of the command line editor.

## 23. Draft and ready preset review

`BlockCheck2PresetDraft` keeps two forms:

- the structured command line from the automatic or manual builder;
- a temporary expert command line.

The `Origin` value is `Automatic`, `Manual` or `ModifiedAutomatic`. The structured assignments are the source of truth until the user enables expert editing.

On `PresetReviewPage`, the command line is read-only by default. The "Allow editing" flag enables the expert mode and the ConfigMaker command bar after a confirmation. A return to the builder after a change of the expert line requires a confirmation and deletes the expert changes. The reason: the add-on cannot convert free text back to assignments without data loss.

The "Used files" and "Preset groups" panels are a part of the editor:

- the files are in groups: TXT lists, libraries and payloads;
- the groups show the common filters, the profile filters and the sequence of actions;
- for Zapret2, the flags get explanations of their actual values;
- a double-click on a group element moves the editor to the exact occurrence, also for duplicate flags;
- the panels change when the command line changes.

For a file, the commands "Open", "Show in folder" and, for a missing file, "Replace" are available. The editor opens a TXT file with the application from the settings. If no application is set, `EditSitelistAskApplicationContentDialog` opens.

The replacement of a missing file uses `ConfigImportAutoCorrector`:

- the user selects the automatic correction or a file;
- an empty file can be the recommendation;
- the editor replaces all occurrences of the original path, not only the first one;
- the file tree and the groups change after the replacement.

`ConfigMakerUserControl` gets the Zapret2 help through the tray and ConPTY. Thus it can also use components that require UAC. The parser restores the logical lines from the terminal control sequences, the help groups, the continued long descriptions and the flags without a description. An unknown `--help` is not fatal if the process shows the list of options.

The editor has search, replace, zoom, font, background, format by flags, warnings and errors without distracting animation, and a temporary run of the config.

## 24. Preset save

### 24.1. TXT

The user can save the current effective line as a usual TXT file. This does not change the CDPI UI storage.

### 24.2. In the application

Before the save, the user types a name. `BlockCheck2PresetStorageService` does these steps:

1. It makes sure that the preset is not empty and that Zapret2 is installed.
2. It finds `--hostlist`, `--hostlist-exclude`, `--lua-init` and file `--blob` values.
3. It resolves absolute, component-relative and `$GETCURRENTDIR()` paths.
4. It keeps libraries and payloads in the Zapret2 folder as component-relative paths.
5. It copies external lists to `LocalUserStorage/List/BlockCheck2/<storageId>`.
6. It copies external libraries and payloads to `LocalUserStorage/Bin/BlockCheck2/<storageId>`.
7. It changes all references to `$GETCURRENTDIR()/List/...` or `$GETCURRENTDIR()/Bin/...`.
8. It makes a user JSON `ConfigItem` for Zapret2.
9. It refreshes the config list of the component.

The service keeps a Russian or other Unicode preset name as the display name. It transliterates the folder ID and the JSON name to ASCII, removes unsafe characters and adds a Unix time, for example `Novyi_preset_1786437896.json`. It also transliterates the names of the copied files. A name collision gets the suffix `_2`, `_3` and so on.

If an error occurs, the service deletes the incomplete dependency folders. It does not change the original user files.

## 25. Localization

The main strings are in these files:

```text
CDPIUI/Strings/ru/Resources.resw
CDPIUI/Strings/en-us/Resources.resw
```

The Russian word "Пресеты" is "Configs" in English. New UI elements must get their text through `l:Uids.Uid` or `ILocalizer`. Do not translate technical IDs, issue codes, report keys or strategy IDs.

## 26. Tests

The solution has no separate test project for BlockCheck2. `CDPIUI.Tests` has no BlockCheck2 tests at this time. The core does not use WinUI or the tray. Thus tests can use fake `IBlockCheckStrategyRunner`, `IBlockCheckProbeRunner` and `ICommandExecutor` implementations. `CDPIUI.AddOns` gives `CDPIUI.Tests` access to internal types through `InternalsVisibleTo`.

## 27. Known limits

1. A curl success proves only the exact URL, protocol and IP version. curl does not model ECH, the ClientHello size, the HTTP/2 or HTTP/3 fallback of a browser, subresources or JavaScript.
2. A service can stay unavailable in a browser when the main page passes but the list does not include the API, CDN, video or media hosts, or the other IP version.
3. QUIC is in the model and the catalog. The standard UI hides it until the user scenario is complete and the HTTP/3 support of curl is verified.
4. Discord voice and random UDP are not supported. They need a different probe: address and port capture, a controlled client start and a criterion for a real media handshake.
5. The automatic circular fallback can depend on the network state. Thus the combined check is mandatory.
6. A text report is less precise than JSON. Use JSON for later editing.
7. An imported report can refer to local files that are missing or to strategies of a removed catalog version. The user can view it, but the preset needs a repair.
8. The temporary per-site lists stay while an available result refers to them. When the user saves the preset, the service copies the dependencies to the permanent user storage.

## 28. Extension points

To add a new probe transport, implement `IBlockCheckProbeRunner` and, if necessary, `IBlockCheckProbePreflight`.

To start the component in a different way, implement `IBlockCheckStrategyRunner`. The add-on core does not depend on WinUI or the tray.

Add new strategies to the seed JSON or to the catalog expander. Each strategy must have correct applicability, tier, cost, ordered actions and circular, pre-host and inbound flags.

A new report format requires a higher `SchemaVersion`, a migration in `BlockCheckReportImportService` and test examples. The import must not accept an unknown schema silently.

If you change the mode parameters, keep `Scan.AttemptsPerTarget` and `Validation.AttemptsPerTarget` not less than `Synthesis.MinimumAttempts`. Otherwise, the preflight returns `RUN_ATTEMPTS_MISMATCH` or `RUN_VALIDATION_ATTEMPTS_MISMATCH` before any process starts.
