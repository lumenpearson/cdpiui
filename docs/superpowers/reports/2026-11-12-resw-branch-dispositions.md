# RESW separation branch dispositions

This is a chronological evidence record. Earlier readiness statements retain their original stage. See [the 26 November reconciliation](#rollout-reconciliation-on-26-november-2026) for later ownership and execution boundaries.

Owning tasks: #235 (frozen JSON beta) and #236 (stable RESW and active rollout).

## Verified base and current owned work

The initial remote main is `fe29acfc4238228da3a6aebb98fff4345c3da106`. The current-main catalog manifest contains26 catalogs and5330 entries, including20 GUI catalogs/5206 entries. The old primary-checkout test result is not the baseline for this change.

| Branch | Owner-confirmed disposition |
|---|---|
| `dev/beta-json-localization` | Frozen locally at `19b221428d7ed76c68e9e0cd0a6a50150ae9701d`. Its product tree is identical to fe29; only `docs/features/json-localization-beta.md` differs. It is the beta exception. |
| `dev/resw-localization` | Corrected product/test revision `50a883d6ff48f77d0da49435376c0e8b6fa05e88` passes the final local checks recorded below. The earlier a688017 results remain historical. Final documentation does not change the tested product. External delivery gates remain separate. |
| `main` | Stable adoption target after reviewed PR, native/CI acceptance and restored Project admission. No integration has occurred. |
| `dev/class-stack-integration` at08f6 | Owner01a1c5f4 confirms frozen accepted #14/PR233 evidence. Preserve ref, checkout, index and ignored evidence. |
| `dev/neutral-config-utilities` at249bba | Owner01a1c049 confirms frozen historical/local verification input. Preserve in place. |
| `dev/welcome-migration-presentation` at5c7a945 | Owner/coordinator01a1bc96 confirms frozen published PR232 input, with protected PR230 ancestry. Preserve in place. |
| Future `dev/presentation-owner-integration` | Does not exist yet. Owner01a1c049 will amend #234's baseline and resource/native contract after actual accepted RESW main delivery. It is not updated implicitly. |

## Remaining classification and rollout boundary

The frozen-input receipts are retained in the shared queue and the #235/#236 case records. No frozen PR is retargeted or reported merged merely because its commits are reachable. Earlier policy/decomposition/localization refs and backups remain historical until their actual owners/activity are fully reconciled; a branch prefix alone does not establish active work.

The primary checkout remains on local main7ead95f with preserved localization WIP. Its index hash was captured before work. It must not receive a reset, stash, silent history move or whole-tree overwrite. Any later owner-controlled source cleanup must preserve unrelated staged/unstaged and ignored files and prove that moved localization work exists in the beta/evidence.

Initial Browser Use attempts denied GitHub at runtime. The user explicitly allowed local code/branch/build/review preparation despite that metadata prerequisite. After the user changed the browser permission and restarted Codex, a separately admitted diagnostic session 215 restored Project 7 read access. Its release 216 is recorded by the coordinator. Writes and intake for #235/#236 remain unverified. The local exception does not authorize a browser workaround, a native UI lease, main integration or completed/Done closure without the remaining acceptance. Shared UI owner.json remains sole-writer coordinator state; this actor has no write authority there.

## Verified local RESW candidate

The implementation revision is `a6880175c5b10c979eb9d1f4a163c0fc23bf1088`, tree `88ed3292ac2f6c44c9395b45d6c2d41b8feb2a9c`. The execution worktree is `C:/Users/lumen/.codex/worktrees/resw-localization/cdpiui`. The actor is chat `01a1d09c-7a6a-7f23-ad87-194d87d35762`. The coordinator admitted the serial CLI cohort with `resw-heavy-cli-window-20261112-180940Z.json` after actual UI release 214. No application, tool executable, browser, installed product or native acceptance session was launched.

The environment used task-local .NET SDK 8.0.428 and Visual Studio 2022 MSBuild 17.14.14 on Windows x64. Every compiler command used one worker, disabled node reuse and disabled shared compilation. No private secret file was copied. The new owned worktree used the documented ignored empty secret stub.

The retained evidence directory is `.superpowers/sdd/2026-11-12-resw-localization/green-a688017/`. These files are local execution evidence, not committed release artifacts. Each step has its exact executable, argument array, output, result, PID and source revision. The source manifest contains 1609 tracked file hashes and remained unchanged across all seven commands and resource inspection. Its SHA256 is `28a6e2fa9df7cca438535898f187429186a30d2506529d3f9573c90d72fcf1a5`.

| Fresh check on a688017 | Result |
|---|---|
| Focused RESW, catalog, emergency and language tests | 38 passed; 0 failed; 0 skipped. |
| Complete `CDPIUI.Tests` suite | 2598 passed; 0 failed; 0 skipped. |
| Normal GUI Release/x64 Build | Exit 0; 73.24 seconds. |
| Normal GUI Release/x64 Publish, win-x64, framework dependent | Exit 0; 69.72 seconds. |
| Tray framework-dependent single-file publish | Exit 0. |
| Update framework-dependent single-file publish | Exit 0. |
| EditHostFile framework-dependent single-file publish | Exit 0. |
| Source/build/publish UI catalogs | All 20 catalogs and 5206 values match the pinned current-main JSON values exactly. |
| Shared resources and Shared inside the actual Tray bundle | All 26 catalogs and 5330 values match exactly. |
| Resources inside actual Update and EditHostFile bundles | Two catalogs each; 28 and 20 values respectively; exact match. Neither standalone tool bundle contains `CDPIUI.Shared.dll`. |
| Actual tool publish directories | One executable per tool, apart from optional PDB files; no external application DLL is required by that layout. |
| Compiled main PRI maps | 2241 English and 2241 Russian values in both build and publish; exact match, including line breaks. |
| Installer resource paths | All 20 unchanged `Strings` paths are present in `NewSetupFiles.wxs`. This is a source inventory check, not an MSI build or installation test. |

The focused TRX SHA256 is `2e8b2d008dd196232e4ede472b25da69cc3a920e5c9339ad3caa8854c0b99923`. The full TRX SHA256 is `aac274173c9ca7762e5dcbf3c2fcfdede6df555068eeaf5495027a35208ccd9d`. Build and publish `resources.pri` are byte-identical, SHA256 `e79731caeaf5df7cd0b5c6754513e483ec3b2d71d5eb8a2ecc25a3e7f9df24ea`. Two bounded `makepri dump` commands completed with exit 0. Resource-only assembly inspection read manifest streams from the produced Shared assembly and assemblies extracted from the actual single-file bundle manifests; it did not call product methods.

The GUI build and publish each emitted 220 `WMC1506` warnings. The JSON baseline has the same warning code and count. This change does not claim a warning-free GUI build. Standalone tool publishing and managed tests did not report compiler warnings in their captured standard output.

The baseline is a separate actual run at product revision fe29: 2590 tests passed, and normal GUI Build/Publish completed with exit 0. No baseline test count is presented as a RESW result. The eight additional RESW cases account for the current 2598-case suite. Timings from these serial runs are observations, not a development-speed benchmark: incremental state and caches differ.

## Recovery tests and preserved behavior

The runtime RED executed once before implementation: 17 cases, 13 expected assertion failures, 4 passes and 0 skipped cases. The old JSON reader rejected the XML fixtures. No compile or restore failure was counted as RED. Its complete 1609-file source archive and TRX remain in the task workspace. The archive SHA256 is `22bc865e562eb8bc5f72693ec257ce6e4bccfcfb3eb339859d7df3775b0bb702`.

The new reader prohibits DTDs and external resolution. It limits each XML document to 4,194,304 characters and rejects malformed roots, entries and duplicate names before it publishes a cached dictionary. Missing, blank, damaged and unreadable selected resources use English; failure of both catalogs returns the resource ID. It does not call logging, settings or UI during recovery. Process-fatal exceptions retain the existing exclusion.

The conversion preserves every current-main key, translation, format argument and CR/LF sequence. Four inherited translator comments are restored. `LocalePreference.cs`, `App.xaml.cs`, `AppLocalizerInitializer.cs`, `LanguageSelectModel.cs` and `EmergencyRecovery.cs` remain byte-identical to fe29. No XAML or branch-specific presentation implementation changed. The primary checkout index retains its captured SHA256 `42c36f427f08ff07ca2e8872ac2995b44f174bcd71a4815b23072d11dee197a3`.

The first immediate final PID check observed a recorded process and did not write an idle receipt. An initial premature coordination message was explicitly withdrawn. Subsequent fresh identity and final CIM checks found all 62 recorded PIDs absent. The qualified idle receipt records this correction. No process was force-killed and no successful verification command was repeated.

## Documentation and remaining acceptance

The first local governance check correctly failed on three missing inventory records for this task's specification, plan and report. The final documentation update adds their exact first-introduction metadata. The 32 governance unit tests passed before that correction. Final governance and independent review must be recorded against the resulting documentation revision.

This remains a local preparation result. Documentation commit e6b5432 did not change product code, tests, project inputs or catalogs from a688017. The independent review below verified that boundary. Native EN/RU switching, restart and emergency acceptance; installed-product and MSI qualification; actual Project 7 fields; hosted PR checks; main integration; every active owner disposition; and issue closure remain pending. Frozen refs and other owners' worktrees retain their recorded identities. Do not accept #235 or #236 from this local record alone.

## Independent review and the single correction pass

Fresh reviewer `/root/resw_final_review` reviewed the complete range from fe29 to `e6b5432171aed8b0b9ee7fbde3b552d7ecc702a1`. The reviewer independently compared all 26 catalogs/5330 values, both PRI maps and the actual TRX counters/hashes. No critical defect was found. No additional localization behavior was declined as outside the design. The reviewer retained native, installed-product, hosted, Project and integration gates as incomplete.

The important finding was missing checked-in coverage for project resource delivery. The original artifact inspection proves the maps existed on a688017, but the managed suite did not guard the two PRI declarations. Two new project contract tests now check the 20 GUI Content destinations and copy settings, both PRI links, 26 Shared manifest names and two manifest names per standalone tool.

The negative control temporarily removed only the Russian PRI declaration in this owned worktree. A fresh bounded run executed two cases: the GUI layout test failed specifically because `Strings/ru/Resources.resw` was missing, and the Shared/tool manifest test passed. There were no skipped tests or compile/restore failures. The dotnet process exited 1; its wrapper exited 0. TRX SHA256 is `4a23b6c1010d5e8201582be1f66d3c81dab7e014547d4a9ac3940e23b1f588cd`. All 17 recorded process identities were absent before the coordinator released this RED reservation. The complete 1610-file RED archive SHA256 is `47040f59f7bfc58b82b58d367a3875c14fc7a6ec3c6314b713f82d0c4a042064`.

After actual release, the exact original GUI project bytes were restored. The final change does not omit or alter either PRI declaration. The new tests remain in the candidate. Their successful execution and a fresh complete suite are required before the correction is locally ready.

The reviewer also reproduced `git diff --check fe29acf e6b5432` exiting 2 on 28 inherited trailing-space lines in six RESW catalogs. The earlier successful check covered the working-tree delta and did not establish branch-wide cleanliness. That broader implication was incorrect. Although the reviewer labelled this minor, the executor treats a failed required delivery check as important. Each flagged line-ending space is now written as `&#32;`, with identical decoded translation values and newlines. No translation was trimmed.

The original a688017 GREEN evidence remains historical. The final checks below verify the correction on its own sealed revision. The one author correction pass follows the review; no second independent reviewer is dispatched. No new runtime lookup, language, startup, caption or toggle behavior is introduced by this pass.

## Final local result on 50a883d

The tested revision is `50a883d6ff48f77d0da49435376c0e8b6fa05e88`. Its source manifest contains 1610 files, SHA256 `71d72296ae1040759c06db2fbd58b2347ab79f41862637fe1eaf5c85d9b47bdc`. The working tree was clean and every source hash stayed unchanged through both execution windows and final inspection.

| Actual check on 50a883d | Result |
|---|---|
| Focused localization/project-layout suite | 40 passed, 0 failed, 0 skipped. |
| Complete managed suite | 2600 passed, 0 failed, 0 skipped. |
| Normal GUI Build and Publish | Both exit 0; 59.87 and 57.62 seconds. |
| Tray, Update and EditHostFile single-file publishes | All exit 0; 6.01, 2.83 and 2.56 seconds. |
| Loose GUI resources | 20 catalogs / 5206 exact values in source, build and publish. |
| Shared and Shared inside the actual Tray bundle | 26 catalogs / 5330 exact values each. |
| Actual standalone tool bundles | Update: 2 catalogs / 28 exact values; EditHostFile: 2 / 20. No Shared application DLL is in either standalone bundle. |
| Compiled main PRI | 2241 English and 2241 Russian values, exact in both outputs. |
| Installer resource source inventory | All 20 expected paths remain present; no MSI installation is claimed. |

The focused TRX SHA256 is `4fbf851dfe84abcce4068d6cb6aeed56b2927f45514e24d8b611882cc10a9e8e`. The full TRX SHA256 is `1f96d830f4df87475a7c169ff347507bca67441bf16b98295af58821c669dc20`. Both compiled PRI files retain SHA256 `e79731caeaf5df7cd0b5c6754513e483ec3b2d71d5eb8a2ecc25a3e7f9df24ea`, identical to the original local RESW result. Each GUI command still emits 220 WMC1506 warnings, as the JSON baseline did.

These are **split-cohort** results. Reservation `193539Z` completed the focused tests, full tests, GUI Build and GUI Publish. Its immediate post-Publish process guard then observed a recorded PID and stopped before any tool publish. The exact transient identity was not printed, so the cause is unknown. No test or build failed. Subsequent fresh process checks found all 47 recorded PIDs absent. The wrapper exit 1 and partial stop remain recorded; this was not a complete seven-step run.

The coordinator released that window. Separate reservation `195153Z` admitted only the three previously unattempted tool publishes. The continuation captured PID, creation time, executable path and parent information, compared identities and allowed up to ten seconds for normal exit. It did not kill a lingering or unrelated process. All three publishes and the continuation wrapper exited 0. The four successful checks were not repeated.

Before continuation, a 1515-file manifest pinned all prior logs, TRXs and GUI build/publish deployment files. Its SHA256 is `08e9f71fb67a2153fcd6ba185343637a9818f389e605915474bf2c060a51bb9e`. All 1515 hashes matched before and after continuation and after final inspection. The previous four-result record has SHA256 `e5888c02e22b5794767450628c353ad07a6695704a250457457fb2e5b9056648`.

Only after the three new commands succeeded were the two bounded PRI dumps and resource-stream/bundle inspections executed. All 1610 source hashes still matched. All 16 recorded continuation/inspection process IDs were absent at final qualification; no process was force-killed. The coordinator independently verified the source/deployment closure, results and PRI maps, then released `195153Z`. No application executable, product method, native UI or installer ran.

The retained final evidence is in two new directories on F:

- `F:/CodexArtifacts/cdpiui-resw-20261112-50a883d-01a1d09c`: four successful commands and the original guard stop.
- `F:/CodexArtifacts/cdpiui-resw-20261112-50a883d-01a1d09c-tools`: three tool publishes, combined artifact proof, extracted bundle metadata and PRI proof.

Only result/publish/inspection destinations changed to F. Normal test bin/obj and `AppContext.BaseDirectory` remained under the repository on C. No `--artifacts-path`, `BaseOutputPath` or `OutputPath` override was used. No old evidence was deleted or relocated. The observed reference cohort used about 180 MiB of retained output; the new request reserved an estimated 256 MiB and used explicit free-space guards. These estimates were not upper bounds.

The first attempted wrapper call had an incorrect reservation filename and failed at file read before any managed child or output directory started. The coordinator separately requalified the first actual launch through a canonical path binding. The caller error remains a preflight failure, not a failed or repeated test.

## Actual Project intake and remaining integration

An authorized metadata executor completed the targeted #235/#236 intake under UI grant 225. Each card was reloaded independently. The source owner viewed both retained PNGs and accessibility snapshots and verified all four file hashes. The coordinator qualified release 226 and explicitly returned metadata ownership.

| Issue | Actual item | Status | Priority | Area | Risk |
|---|---|---|---|---|---|
| #235 | 302888154 | In progress | P2 | UX | Medium |
| #236 | 302888676 | In progress | P2 | UX | Medium |

Fresh GitHub API reads confirm both issues are open, assigned to `lumenpearson`, with milestone `G4 — Interface and documentation` and exactly `type:refactor` / `area:ux` labels. This actual intake followed the user's explicit local-preparation exception; it does not claim private fields were verified before implementation. Historical access failures and the original contract remain visible.

Local code preparation, the independent review and its one correction pass now have their required managed and artifact evidence. The source owner's final documentation commit must remain separate from the tested 50a883d product/test tree. Whole-branch whitespace and governance checks apply to that final documentation HEAD.

Main integration and active-owner adoption are incomplete. Native EN/RU/live-caption/restart/emergency acceptance needs a separately admitted controlled fixture: normal startup registers the global application instance key and can register current-user file/protocol handlers. A temporary publish path is not an isolated product fixture by itself. Do not launch the product casually under a metadata-only grant or modify registry/installations to claim a smoke result. Hosted checks on the final PR HEAD, any required packaging/installed-product acceptance, main merge, owner-controlled rollout and paired issue/Project closure remain pending. The JSON beta and all frozen owner refs remain preserved.


## Installed RESW matrix on the corrected candidate

The native product revision is `cd5bcfd9a059f287b9cc851fb5a33845280c3cbe`, tree `a7da6a6f356156145a5d17540099d4fd3c9fb068`. It contains the RESW candidate plus the separate Tray font correction #238/PR239. The source operator remained at clean `ffae24346fa80ed5a6fc4a5e2ae1de9c000aee21`; the two source roles are explicit. The selected push artifact is14062464409 from successful workflow43476971367. Its ZIP SHA256 is `122411caa6168d7457bf03e24f8ca12c49535332332b449cdabd70075c9bb47f`; the MSI SHA256 is `e5b9e8bd463b4dce97b59d7040676ce846a9fb4c43e8deb09a5d98c145578bb6`.

The corrected package inspection completed five actual commands with exit0. It measured322 installed files/156215630 bytes, all20 loose GUI RESW catalogs/5206 exact values,26 Shared catalogs/5330 values both directly and inside the Tray bundle, Update2catalogs/28values and Hosts2/20, and2241 exact EN and2241 exact RU compiled PRI values. Its678-file output manifest SHA256 is `e3ef3b17a9c20abec70ed1d97490bdd63f6ad4681b957269e604cf2158bb4276`. The generated package manifest outside those four output directories is pinned separately at `08d05b570c9d4a6824c825011282b39d070f5332247c34ade7e64fc681340b0d`. Process sampling was bounded: a reused unrelated PID was qualified by different creation time/path/parent without controlling it, and the final short-lived inspection child's birth was not captured. Successful command exit and later absence remain distinct evidence.

Actual exclusive native grant275 ran one new offline Windows Sandbox under the immutable67-file seal `f9568a726faf9c7c8e9f42d560845192778647b6233a506ef6013c976d0f2a76`. Initial runtime and MSI installation returned0. All322 initial installed payload hashes matched the expected MSI manifest. The four existing compiled fixtures retained their original build provenance and consumed the measured installed `CDPIUI.dll` SHA256 `873c2ecb2a39f14770296d1b76c62de8535775b2100119d3daa1ad398c2e89eb`.

| Cases | Actual observed behavior |
|---|---|
|1–3|Original EXE EN→RU live selection, persisted RU→EN live selection, English restart/About version1.0.4.0MSI-BUILD; readable Tray text and actual normal Tray exit.|
|4–5|Real production dynamic toggles retain On/Off and Вкл./Откл. across live EN/RU changes; unchanged settled fixture PID and birth.|
|6–9|Real critical window remains readable with preserved synthetic details and code after confirmed localizer failure and, where requested, settings-read failure.|
|10–11|Real EN/RU error dialogs show readable controls and expanded `NATIVE_TEST_EXCEPTION: synthetic details`; actual OK dismissal then normal fixture host close.|
|12–13|Broken exception formatting retains the exception type and `ERR_INTERNAL` in the real readable critical window.|
|14–15|Original installed EXE without Strings shows readable EN/RU emergency controls, original missing path, `ERR_INTERNAL_IO_DIRECTORY_NOT_FOUND` and version1.0.4.0. Actual Tray exit while the emergency window is open completes both processes.|
|16|After Strings restoration, original EXE returns to normal English Home and readable Tray; actual Tray exit completes both processes.|

Every case has an actual normal case-specific exit followed by a bound `inspect-owned` count0. Cases1,2,4,5 retain settled PID+creation time+path+parent snapshots before and after the actual live change. Initial launcher handoff is bounded snapshots, not a continuous trace. Tray language reflects its own startup language; the run does not claim that an existing Tray instance changes language live.

Cases8 and12 start in English but then make the selected preference unreadable. Their actual emergency language is Russian under the existing Windows-culture fallback. Root and a distinct peer classified this against unchanged `LocalePreference.Read` and `FailureReadingPreferenceUsesWindowsLanguage`. No input or criterion changed and no case was repeated. This does not prove English emergency UI after settings-read failure. Cases9/13 also observed Russian. Broken-error fixtures return before the visual-tree timer; actual frames plus actual failure events supply that evidence, without invented fields.

The final guest `finish` reports `state=finished`, `checkedFiles=322`, `productCount=0` and14 actual guest input hashes, each matched to immutable host bytes. These hashes belong to finish, not a capture report. Normal own Sandbox Close and its visible disposal confirmation were observed. Fresh own-window, launcher, client and monitor absence was recorded. The monitor wrote `normally-finished`; a raw OS monitor exit code was not measured. All833 protected host files,16 registry roots and the primary index remained identical; protected foreign PID17312 retained its original birth and was untouched. All67 current sealed inputs and nine historical cohort seals remained unchanged.

The source result is `2672135f6c65d5ae58a4cd6538c5bf8bcf6771be1e63582b5e35065a49eab016`. The immutable retained manifest is `520b5ef7b6de2d505cfb22c7e760ffed885a231069c052ff6db79e7af435b20e` (355 files+4external receipts,62 private original own275 images). Actual coordinator release276 is a later external receipt, SHA256 `1427e60ae6daeaa8f97d621f52f75298a8f5aee0b9cead0044609ac4d231726e`. The sole independent retained review subsequently accepted all 16 scenarios without blockers. Its receipt SHA256 is `eac00d398998a3bea99e1081b87fabbf85a95408d4b9ce08fe45d802d8d0de04`, with separate data receipt `5870c2d0b3da950d3e6a1a23729360f71a86e96da6922999ddc311086c3c45e7`. The reviewer personally assessed all 61 unique original contents among the 62 permitted paths. The original observation-stage acceptance count remains unchanged; this additive receipt supplies acceptance. One external resource slot pin describes its archived active bytes before actual release; the release-only transition was verified separately.

Operator-analysis failures remain visible: one case9 read collided with periodic JSON writing and the final after-exit file parsed; one case12 script assumed an absent visual-tree event and was corrected against actual source/frames. An initial extensionless monitor marker was ignored; the exact watched `.json` marker then triggered normal finish. No guest command failed or was repeated for these corrections. No backend kill, host restoration, new capture probe or provider change occurred.

These results do not establish historical261 black-screen root cause, continuous rendering, keyboard/screenreader/clipboard behavior, upgrade/rollback/uninstall, broad G3/G4/G5 acceptance or release readiness. The independent retained native gate is accepted. Final reviewed PR HEAD/CI, main integration and active-owner adoption remain separate gates.

## Normal font integration and final source boundary

PR239 merged normally into `dev/resw-localization` at `5c280cce00d9e74a04428b9d23497a5bd60a266e`. Its parents are exact `ffae24346fa80ed5a6fc4a5e2ae1de9c000aee21` and tested `cd5bcfd9a059f287b9cc851fb5a33845280c3cbe`. Its complete tree is the same tested `a7da6a6f356156145a5d17540099d4fd3c9fb068`. The source owner verified GitHub API and `git ls-remote`, then fast-forwarded only the clean owned RESW worktree. No squash, rebase or source replay occurred.

The final documentation pass updates this report, the RESW and Tray plans, their inventory records and one exact source comment from `Embedded JSON` to `Embedded RESW`. Reverting that single literal comment produces the exact native source file. All other product, test, project, workflow and catalog bytes must remain identical to cd5. The independent final integration reviewer and exact final-head CI must verify this boundary before PR237 merges. No new native execution is inferred from a comment or prose change.

The JSON beta remains frozen at19b221 with original fe29 product bytes. The later common Tray font correction238 is a separately named difference; it is not silently attributed to JSON localization and was not added to the frozen experiment. Eight presentation refs remain frozen historical inputs. Root owns the new font branch atcd5. The foreign primary main remains local7ead95f with its original WIP and index SHA256 `42c36f427f08ff07ca2e8872ac2995b44f174bcd71a4815b23072d11dee197a3`. It needs owner-controlled adoption; a remote main merge alone cannot complete all-active rollout236. Accepted reviewed main can separately unblock234, without waiting for broad236 closure.

The local `origin/main` tracking ref was stale at21555a29. Fresh GitHub API and `git ls-remote` agreed on actual remote fe29. A bounded explicit-main fetch with an empty refmap fetched fe29 without changing local main. A compare-and-swap updated only the tracking ref from the recorded stale value to actual fe29. The primary branch, checkout, index and WIP were not changed.

## Rollout reconciliation on 26 November 2026

Fresh connected GitHub API identifies main as `99e3701f00ac1b7045238b6b2c99f2073d15eb9b`. The coordinator read back #14 and #234 as completed. Initial descriptions of unmerged stable main, a future presentation checkout or #234 In progress are historical. Accepted stable RESW integration at `d2116756`, final source `9a266970` and native cohort 275 (16/16) retain their original exact scopes; none was executed again for this reconciliation.

### Owner-controlled execution dispositions

Owners explicitly supplied the following dispositions. Preserve each frozen HEAD, index, WIP, ignored fixtures and evidence. No frozen branch is merged, deleted or advanced to increase a rollout count.

| Branch | Exact HEAD | Owner and current execution disposition |
|---|---|---|
| `dev/class-stack-integration` | `08f6de285617cfb5eaac58f91eda31d336280063` | Owner `01a1c5f4`: frozen accepted #14/PR233 evidence; no active execution. |
| `dev/welcome-migration-presentation` | `5c7a9459589b6d8c18a794d675c29ac0ed285e54` | Root: completed accepted PR232/#234 snapshot; inactive execution target. |
| `dev/tray-font-lifetime` | `cd5bcfd9a059f287b9cc851fb5a33845280c3cbe` | Root: completed accepted #238 font snapshot; inactive execution target. |
| `dev/neutral-config-utilities` | `249bba0143f5929ce5cb75fc09a4ef778aede731` | SourceSecurity: preserved historical #234 snapshot; no active task. |
| `dev/presentation-owner-integration` | `793f7319f225cd4abddc69e8cda8b8dca6323a03` | SourceSecurity: accepted historical snapshot, PR240 merged; no active task. |
| `dev/security-remediation-wave1` | `62d7219b4efa3177fe0438af75fd03ae2853f822` | SourceSecurity: active PR250 candidate; Draft and unmerged. |
| `dev/resw-localization` | `9a266970ebb52de5b1b50bed0316d556c5ae1255` | SourceRESW: accepted PR237 source snapshot; unchanged. |
| `dev/beta-json-localization` | `19b221428d7ed76c68e9e0cd0a6a50150ae9701d` | Frozen JSON exception; governance companion `9071ef3` is also preserved. |

### Root's eight original presentation stages

Root explicitly confirmed all eight original stage heads on 26 November as `FROZEN_ACCEPTED_EVIDENCE_NOT_ACTIVE_PRODUCT_DEV`, with no active execution task. The basis is Root's own retained stage-creation ledgers, issue #14 freeze records and fresh matching local/published Git ref readbacks. Source rehashed the two named owner receipts; the older source map alone was not treated as proof of ownership.

| Historical stage branch | Exact preserved HEAD |
|---|---|
| `dev/audit-class-integration` | `36235846fb116acad49b328b29120afa92714712` |
| `dev/blockcheck-completion-presentation` | `3da11cb098d3e1241b2a86cd86ee0c14d80b1942` |
| `dev/blockcheck-progress-presentation` | `3c7f8961477dbff0b4b021493b629ba1a1a0fa05` |
| `dev/blockcheck-review-presentation` | `b627a0f62dace8dde20ca2574a9d28646f71b421` |
| `dev/blockcheck-wizard-navigation` | `12ef689e0968bd7ff4b27cee0fe7af02091286da` |
| `dev/store-download-presentation` | `3afa82e3f4947ce4bef38b14d86c642c93ddbe90` |
| `dev/configmaker-status-presentation` | `b0f2c93d66000eb02cd963cf4421173980686e95` |
| `dev/welcome-migration-presentation` | `5c7a9459589b6d8c18a794d675c29ac0ed285e54` |

The current owner-disposition receipt is SHA256 `abb58e061e142b42f30d1a98d40280a548d25b0f0750d255c44aec0f0e2d9e79`; its supporting input/ref readback is `81ab42311f8cad764296c03a954f291272be99dd371202731bd70652431fc7a3`. These eight are excluded from in-place RESW execution by the owner's explicit decision. Preserve all original heads, indices, WIP and ignored evidence. This decision does not close or merge historical PRs or infer their status from reachability.

An open Draft PR does not prove active execution. Other legacy refs keep their recorded or unresolved dispositions; do not infer activity or inactivity from names, reachability or archived chats. This is not a claim that every historical WIP feature was integrated.

### Active security candidate retains RESW

PR #250 at `62d7219` has direct parent main `99e3701f` and tree `fdb559f0b562fdbe323c9442a5d1a7758f2b939f`. Root independently checked all 26 RESW blobs and 12 build-input blobs against that parent. All are byte-identical. Decoded checkout catalogs retain 5330 values, 2665 per language, and translator comments after normal CRLF conversion. Receipt SHA256: `ada3e3d7324b8799249e474acee27ffa00cd1af507924c7c562a12805b59465b`.

Existing exact-head hosted CI passed 2699 main and 10 font tests with zero failures or skips; receipt SHA256 `ddd9450c3e635f186ee8d1db29dd829d606f1a6d4ed28c9b33c9523f3d690f08`. The prior five scoped native security groups have receipt SHA256 `f722439188ab37530bcc0b2d7e6a6eb360cce77cb39799452ce3ed1fd21f6bd9`. These receipts were read and rehashed, not rerun. Static inheritance does not establish a new compiled PRI, resource UI, installation or global release result. PR #250 remains unmerged and its current private Project fields are unknown.

### New custody of the preserved primary

The maintainer explicitly requested a preserved transfer of the primary working copy. Root appointed SourceRESW (`01a1d09c-7a6a-7f23-ad87-194d87d35762`) as the new custodian; SourceRESW accepted this new transaction on 26 November. The historical prior owner and handback remain unknown. They were not inferred from archived chats or supplied retroactively.

The physical primary stays on local `main@7ead95f7756057f67cd113671a7f84cf422e8d32`, index SHA256 `42c36f427f08ff07ca2e8872ac2995b44f174bcd71a4815b23072d11dee197a3`. It is now a **preserved inactive execution target**, not an updated current-main product checkout. No source, branch, HEAD, index, original ACL or path was changed. Its staged, unstaged, untracked and ignored work remains in place. All uncertain or unrelated WIP stays preserved and unapplied; no older JSON overlay was replayed.

One new private preservation copy contains 32851 approved regular files and 3614696751 bytes. Forty-five credential-like files were excluded without reading or copying their payloads; their originals stay in place. Twenty expected deleted old RESW paths have an explicit deletion manifest. The common Git directory was not copied; the exact original index was retained separately. Original files remain the second retained copy. No all-files-copied claim includes the exclusions.

Source completed full source/destination hash verification with actual Python exit0. Root then independently rehashed all 32851 source/destination pairs, checked exact membership, unchanged source pathlists/index and the protected new-directory ACL, and completed its reader with exit0. Preservation manifest SHA256: `4c04fa2724a36942654f718f4785e04e060613a18444bee5bdf5aa87640b7300`; Root review: `4bf008577668bed995cb77d034ba6af8f63616f8d3c8a84809498020610e9b22`; actual serial release: `d75543b1dc6d9b205bf430fb034470bb37a98bfda84b1a0465531f95ca32121e`.

Three wrapper metadata writes failed because `Set-Content -NoClobber` is not a valid parameter combination. The failures remain retained. Separate additive records capture actual argv, PID/birth and measured Python/outer exit0; failed original records were not fabricated and copying was not repeated. Checks are non-atomic per-file observations, not a global concurrent-writer lock.

The semantic disposition review covers nine tracked differences from beta and seventeen from main; all originals remain unapplied. All 5320 older JSON values are retained in the beta/main 5330 catalogs. New custody receipt: `f84579e973f29afbd485a99dd1cc28b0dabc22d254d7caebd3ecbcc9917fdbb5`; independent custody review: `cacd26e6a28c635b75ad8ba1d5169b5a5d21f23b52d252710c22fbfdc7541f5f`. These establish preservation and custody, not full unrelated runtime equivalence or issue closure.

The documentation execution owner is SourceRESW (`01a1d09c-7a6a-7f23-ad87-194d87d35762`). Its manual Git worktree is `F:/CodexWorktrees/cdpiui-resw-rollout-closeout-20261126-01a1d09c/cdpiui`, on branch `dev/resw-rollout-closeout`, based on exact main `99e3701f00ac1b7045238b6b2c99f2073d15eb9b`. Creation preserved all old refs and indices; this worktree is not a Codex-managed attachment. Local documentation HEADs, checks and review receipts are recorded externally in the owning task and private handoff, rather than a circular self-HEAD claim in this document. The accepted `9a266970` source and the legacy physical primary remain unchanged. No public PR or main integration is inferred from this local documentation stage.

### Project access and remaining acceptance

Current private Project 7 fields remain **unknown**. Earlier intake and Blocked/P2/UX/Medium readbacks retain their original dates and the approved local-preparation timing exception. Issue API access and successful CI do not verify private fields. The original before-implementation timing is not invented.

Actual browser attempt 291 was denied by the saved-user/managed origin gate before a Project page loaded. A selected supported configuration diagnosis found only one exact github allow rule, no competing local rule or profile browser override, and no documented Windows legacy managed-config file. The effective source of denial remains unknown; no permission was changed and the origin was not retried.

Read-only session 295 made one supported app-inventory call. The returned package identity contained Codex, but its display name and window title were ChatGPT. That target was outside the Codex-only grant and prohibited by Windows automation guidance. Source stopped before state capture, screenshot, accessibility, input, settings, browser or Project access. The original selected metadata and explicit stop receipt remain private; no PNG or AX result was invented. Root qualified actual release 296. No repaired-access claim follows from this attempt.

This documentation stage changes no product code or resource. Its local checks, independent review, eventual publication/integration and actual Project intake/acceptance must be recorded separately. Issue236 stays open until the current contract and actual issue/Project closing readbacks are satisfied. Broader G3–G5 and installed-product/release tasks retain their own scopes.
