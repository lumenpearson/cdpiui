# RESW separation branch dispositions

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
