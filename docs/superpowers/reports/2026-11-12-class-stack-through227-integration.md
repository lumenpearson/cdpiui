# Stack through PR 227: current integration verification

Owner: chat `01a1c5f4-bad8-7310-bafb-1bcf792172a9`, issue [14](https://github.com/lumenpearson/cdpiui/issues/14), branch `dev/class-stack-integration`, isolated `C:/Users/lumen/.codex/worktrees/future-task-issues/cdpiui`.

[Draft PR 233](https://github.com/lumenpearson/cdpiui/pull/233) is the current integration vehicle. **This is the frozen pre-merge verification record.** The failed batch189 is preserved below. A separately authorized Root-owned R2 subsequently passed host178 and actual-close shutdown checks under grant193. Final review, final-HEAD CI and actual main integration remain delivery gates; the issue acceptance comment records their later disposition.

## Delivery decision and preserved history

The maintainer explicitly requested integration of the stack through PR 227. This supersedes the prior executor-defined local/draft-only publication boundary in the historical 2026-11-11 class-integration plan, design and source report. Those records retain their original pins, results and limits. [The additive issue decision and independent boundary review](https://github.com/lumenpearson/cdpiui/issues/14#issuecomment-6806815231) explain the current boundary.

Issue 13 and PR 10 retain historical G1 reconciliation. The one older GUI optional-default difference and three Core expected-hash signature differences predate this integration. Current-main preservation is required; it is not G1 acceptance or adoption of that older branch. The delivery gates permit G2 alongside G1. Issue 15 retains broader integrated native/real-picker/startup scenarios and does not close with this source stage.

## Candidate and provenance

- Current main: `21555a29ce9fba98c678c445d04ee9f6ce3c85ba`.
- Plan-only first parent: `9c24c8a393ce91799a377dc96f7a3020aca8ed52`.
- Normal merge/product candidate: `e417d4efef05c47393bde549c301cb519577d9bc`.
- Stack tip: `3afa82e3f4947ce4bef38b14d86c642c93ddbe90`.

All six published heads are retained as ancestors: PR 216 `36235846fb116acad49b328b29120afa92714712`, PR 218 `3da11cb098d3e1241b2a86cd86ee0c14d80b1942`, PR 220 `3c7f8961477dbff0b4b021493b629ba1a1a0fa05`, PR 222 `b627a0f62dace8dde20ca2574a9d28646f71b421`, PR 224 `12ef689e0968bd7ff4b27cee0fe7af02091286da` and PR 227 at the tip above. Their published refs were preserved. Later PR 230/232 work and foreign checkouts were not merged, retargeted or modified.

The product/test tree matches the clean read-only merge probe `525418989453cf9ce76edd030102a76972a5fd79`. Its only initial differences were the new plan and inventory entry. The inventory append was the only conflict; all 129 main and 165 stack records remain, with the one new plan record. No product correction was needed. All 16 main test-project items remain; all 130 explicit Compile includes exist without duplicates. All five nonproject PR 229 blobs remain exact. Tray, elevated launcher, Shared, installer, Update, EditHostFile, dependency versions, package sources and workflows remain current-main content.

The state-owner map is in the historical decomposition decisions, accepted per-owner reports and the current source. The aggregate retains the focused editor/document/resource owners, App activation/window/modal/close/idle composition, BlockCheck2 state/result/manual-test/presentation/navigation owners, CreateConfig owners, stateless Core projection and Store download presentation owner. Review checked live reads, collection identity, notification order, cancellation ownership, callback/await sequencing, reentry and partial-failure behavior; no new duplicate domain rule was found.

## Fresh commands and results

Environment: Windows x64, .NET SDK 8.0.427. The managed runs and GUI build use separate platform outputs. Their Core DLL hashes must not be treated as identical.

| Command or check | Current result |
|---|---|
| Pristine main archive: `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release -m:1` | 747/747 actual tests passed; zero failed/skipped; 10 seconds test duration |
| Pristine main: `dotnet build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 -m:1` | 0 warnings/errors; 69.85 seconds |
| Candidate focused command below | 1845/1845 passed; zero failed/skipped; 416 ms test duration |
| Candidate full: `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore -m:1` | 2590/2590 passed; zero failed/skipped; 10 seconds test duration |
| Candidate normal GUI: `dotnet build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 -m:1` | 0 warnings/errors; 65.83 seconds |
| `python .github/scripts/check_governance.py` on e417 | 166 Markdown documents; 0 errors |
| `python -m unittest discover -s .github/scripts/tests -v` | 32/32 passed |

The focused command was:

```text
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release -m:1 --filter "FullyQualifiedName~CDPIUI.Tests.Presentation|FullyQualifiedName~LanguageSelectModelTests|FullyQualifiedName~ConfigurationVariableProjectionTests"
```

Every baseline test display name remains in the full candidate results. Every focused name is also present in that full run. Unique TRX directories preserve each scope separately.

Source syntax inventory retains all 451 original GUI exported types across 520 to 621 Git C# inputs, and all 88 Core types across 102 to 103 inputs. No compared original signature was removed or changed. The prospective-tree API inputs were independently matched to Git blobs; the candidate product tree is identical to that probe.

Fresh compiled comparison against the pristine main binaries retains all 452 GUI and 88 Core exported types with zero lost recorded shapes. Seven added GUI shapes are the already reviewed internal interface declarations on retained facades/storage; Core has no added shape. This metadata surface excludes full annotation, attribute, forwarding and native ABI guarantees. Tests execute the Release/AnyCPU Core output; GUI/API comparison uses Release/x64.

Normal candidate GUI SHA256: `677497652039bf69a93e600b10d1de23b914d335631334e2a3271fe2a21c55d3`. Normal GUI Core SHA256: `c54b8c8d8195ce602e58ec300c9da58124675a82279ea9bc2e25fe61f0a734f0`. The proof records the four separate baseline/candidate binaries and analyzer hash.

The initial baseline attempt in the new checkout failed before tests with CS0234 because the documented empty Secret template had not been generated. The standard script created it; exact comparison with its empty template passed. The first failed log remains. No credential, product fix or staged secret was introduced. The successful pristine archive baseline is the separate result above.

## Independent review and CI

Fresh read-only reviewer `/root/class_stack_integration_review` examined base21555 to e417, the substantive product families and high-risk tests, source/metadata inputs, binary hashes and all three TRX result sets. Verdict: **0 Critical, 0 Important, 0 Minor product findings** at e417. The reviewer did not rerun tests or launch software. The verdict is not merge readiness: final documentation, native and integration gates remain open.

Required checks on e417 were read as successful: CodeQL, Contribution metadata, Analyze csharp, Analyze python, governance, gui, libraries, secrets and tools. Both push/PR executions may appear on that SHA. An initial metadata event before complete PR metadata failed; later events after labels/owner/G2 passed. New documentation commits require their own final-head readback. CI packaging/build results are not installed-product acceptance.

## Actual Project metadata and UI ownership

Issue 14 was opened and reloaded under grant169: In progress / P2 / Architecture / Medium / G2 / lumenpearson, with type:refactor and area:architecture. No field correction was necessary. A pre-UI participant-mapping guard stopped before navigation; the sole coordinator corrected the mapping and retained both records before the successful readback.

The auto-added PR 233 card initially had Inbox and empty Priority/Area/Risk. Under fresh grant175, only that card was set to In progress / P2 / Architecture / Medium. Reload/AX/cell readback confirmed G2, lumenpearson and both canonical labels. JSON and JPG receipts were saved. Only the owner-created temporary tabs were closed. Requests were marked complete/idle; this owner never wrote owner.json. The coordinator controls grants and releases. Private Project fields are not inferred from Actions metadata checks.

After the failed189 batch, browser-only grant191 changed both cards to Blocked. After the separately accepted R2, browser-only grant195 restored both to **Verification / P2 / Architecture / Medium / G2 / lumenpearson**, with both canonical labels retained. Each page was reloaded; actual cell values and PNG receipts were saved. Only own tabs28/29 were closed. This is a real private Project readback, not a CI inference. Final Done remains dependent on actual integration and issue acceptance.

## Native preparation and failed first execution

Two fresh archives of e417 contain 1606 tracked inputs each. Host has 38 declared extras, shutdown 39. Each has exactly 21 reviewed boundary files. All original cut-file inputs match the historical accepted source. The 36 accepted bootstrap files are retained; the main has exactly three declared identity changes: own root, current PIN and log basename.

The fresh host preserves the canonical 178 groups. The shutdown fixture uses two plain no-content owned Window types, actual App registration and CloseWindow/Closed delivery, uncut close-handler/coordinator/cleanup/Low dispatcher/idle forwarding, and an observational wrapper that records state before calling real Application.Exit. It requires a first-close Low FIFO barrier, retained sentinel identity, final empty original registry and exactly one validated real-exit marker. It has no successful unconditional exit fallback. Theme/titlebar behavior and active modal restoration are outside this tail.

The source review caught two Important fixture issues before compilation: old root/PIN literals and a no-longer-isolated theme/logger path caused by Grid content. Both were corrected; previous source/manifests are retained. A preparation-script quoting error stopped before file writes and was corrected. These are fixture preparation records, not product regressions.

Builds: host had 0 warnings/errors in 74.59 seconds; shutdown had 0 warnings/errors in 59.45 seconds. Post-build source hashes and input sets match. Each artifact baseline has 522 files, embedded asInvoker/uiAccess=false, 20 packaged resource dictionaries matching generated bytes and 5206 values matching source JSON. Five offline tail-parser negative controls reject missing, duplicate or premature exit evidence, incomplete cleanup and failure mixed with success. The positive control passes. Optimized Python is rejected.

The native helper requires a fresh direct human native-resume record, a matching new coordinator grant, all other UI participants paused and the reviewed bundle hash. It writes no queue-owner file. At the earlier prelaunch checkpoint, the native user hold was blocked. A run-mode negative control was rejected before preflight or launch. Read-only preflight passed for both packages. No native application had launched at that checkpoint. Independent reviewer `/root/issue14_acceptance_boundary_review` completed the source correction review and final artifact/runner review: **conditional technical PRELAUNCH READY, 0 Critical / 0 Important / 0 Minor**, for bundle `9519CAB19265829F0D1568B14C99C4789B7EF6A8F8C29F28AF730A151C26CF48`. The reviewer independently recomputed helper/manifest/binary hashes, parsed both embedded privilege manifests and compared all resource entries. This is not permission or runtime acceptance. The execution rule required a new direct human native-resume instruction and fresh matching coordinator grant. If the host fails, the shutdown tail must not start; no automatic retry is allowed.

Exact unchanged ConfigMaker/BlockCheck/CreateConfig picker endpoints and view interfaces, combined with controlled-response host tests, justify G2 source-preservation coverage of unchanged picker implementations. They do not prove real picker Cancel. Issue 15 retains real picker, second selection, missing-resource replacement and full integrated scenarios.

## Historical failed native attempt under grant189

The maintainer explicitly approved the two prepared fixtures in direct user message `01a1ce26-0ab5-7b81-9a54-0bf04e27036a`. Both fresh read-only preflights passed, main and PR HEAD remained unchanged, and the sole coordinator issued matching exclusive grant189. The owner read the live grant, scope, request and paused-participant guards before invoking:

```text
python native_runner.py --run host
```

This was exactly one launch of the owned host executable, PID **85016**. It exited **1** after approximately **0.85 seconds**, with `accepted=false`. The log contains **12 PASS groups**, followed by `System.IO.IOException`: `actions-sites.txt` already exists. These partial groups do not count as a 178-group PASS.

The runner precreated all three action inputs before process creation. The retained, unchanged `HostSmokeBootstrap.Actions.cs` then called `CreateActionInput`, which opens these same filenames with `FileMode.CreateNew`. The duplicate creation caused the first collision at the sites input. This is a runner/fixture preparation defect missed by prelaunch qualification. The subsequent read-only audit by `/root/issue14_acceptance_boundary_review` independently confirmed this Important orchestration finding and stated that the earlier conditional PRELAUNCH READY verdict was incorrect for host orchestration. The audit independently rehashed both binary sets, all manifest-listed source files, the bundle and normal GUI; it did not rerun anything. This failure does not establish a product regression. No product or fixture correction was made within that stopped batch. The later R2 is a separately owned, reviewed and authorized package.

The batch stopped immediately, as the human required. **No retry and no shutdown-tail launch occurred.** No file was removed or overwritten. The three inputs, FAIL log, empty stdout/stderr and runtime receipt remain intact. Read-only failure postchecks confirmed the exact owned executable has no process, all 522 baseline host files remain unchanged, the unlaunched shutdown directory still exactly matches its 522-file manifest, both 1606-input source/cut manifests match, and the normal GUI binary hash is unchanged. The host output directory gained only the three runner-created inputs and the result log; no golden report outputs were produced.

Receipts: `native-host-runtime.json`, `native-failure-postcheck189.json`, `human-native-resume-20261112.json`, and queue `issue14-native-idle189.json`. The canonical request is not ready, all owner UI is paused, no call is pending, and the native hold is blocked again. The owner never wrote the queue-owner file. The coordinator alone released grant189 as190. Foreign worktrees and processes were not touched.

The recorded unblock condition required the maintainer to explicitly authorize any later correction and new execution after this stop. A later proposal must resolve input-creation ownership in a fresh package, preserve this failed package, obtain independent review and a new exclusive grant. No automatic resume, repaired-package run, main integration, issue closure or Done transition is authorized by the failed attempt. The prior successful source/managed/CI results retain their named revisions; native criterion4 and integration criterion5 were incomplete at that stopped checkpoint.

## Separately authorized corrected R2 and runtime results

Execution owner for R2: coordinator chat `01a1bc96-ea11-73a1-a4a1-5cd6db1617fa`. R2 uses new `F:/cdpiui-verify/class-stack-e417d4e-20261112-root-r2-host` and `-shutdown` roots. Product source remains e417. The failed189 package, its three inputs, FAIL log, helpers and manifests remain immutable. The corrected runner checks that the three inputs are absent and lets the unchanged host create them. A portable managed control uses the actual two C# helper methods and three literal calls: old preparation has2 failing controls out of7; corrected preparation passes7/7. These controls did not run WinUI.

The R2 host build passed with0 warnings/errors in83.51seconds; shutdown passed with0 warnings/errors in59.03seconds. Each has522 pinned baseline artifacts,1606 tracked inputs,21 declared boundary paths and38/39 extras. Both embedded manifests are asInvoker/uiAccess=false;20 packaged dictionaries and5206 values match generated and source resources. The immutable technical packet has bundle SHA256 `33D32B10E1BFD53EDFDD3EADDD11773062619434DCA956E503E014CCC3F74A58`; its36-input compiled review index is `0F07F238A6FF4E375273AF7DBCF9F8603BD4A5B1C12A9F6EB603A4B8CC09CB3F`. Independent reviewer chat `01a1c049-c090-70c1-bcfe-d38e1e2704fe` qualified that unexecuted packet and then the exact authorized operational-copy delta.

A new direct human answer, actual message `01a1ce58-a45a-7f80-8ad3-e9464a7bb6e1`, approved the concrete two-run R2 question. The issue owner independently read the actual question/answer context and receipt. This approval is distinct from the stopped189 authorization. After fresh peer pauses and preflights, the coordinator issued native-only grant193.

| Actual R2 run | Retained result |
|---|---|
| `python native_runner.py --run host` | One launch, PID96600, exit0,178 canonical unique PASS groups in order, COMPLETE, zero FAIL, accepted=true; runtime5.376586seconds |
| `python native_runner.py --run shutdown` | One later launch, PID92580, exit0,6 ordered events and exactly1 actual-exit marker, accepted=true; runtime3.204804seconds |

The host result includes three exact input byte sequences and two golden export outputs. Its522 baseline file hashes remain unchanged and only the six allowed files were added. The shutdown result has exactly `START → CLOSED_FIRST → FIRST_LOW_BARRIER → CLOSED_LAST → ACTUAL_EXIT → COMPLETE`. It proves original-registry identity, retained first-close sentinel, Low-priority survival barrier, final empty registry and one actual idle-controller exit. Its522 baseline hashes remain unchanged and only its result log was added. Both streams are empty, both owned images are absent, and no retry occurred. The normal GUI, both source manifests, immutable technical inputs and oldfailed189 inputs remain unchanged. The coordinator released the slot as194; all UI remained paused during the CLI-native window.

Runtime artifacts are retained in the Root-owned `.superpowers/sdd/issue14-native-r2-authorized-01a1ce58` directory, outside the issue owner's checkout. Runtime index SHA256: `5B77093817C9881135901F64F8476500AF6C54618A300E71D611793A2B1D6537`. Post-native proof SHA256: `7EEDFF0AAD0FD0D7B70827D0B4D40A3BF7AD961E5E0878990A3F544823B05B78`. This issue owner independently rehashed all16 indexed artifacts and both522-file baselines, checked exact allowed output sets and read both actual PID-bound logs/receipts. That was read-only inspection of the coordinator's execution, not another native run. Independent full-runtime review was subsequently recorded in [issue14](https://github.com/lumenpearson/cdpiui/issues/14#issuecomment-6816245548): `/root/issue14_acceptance_boundary_review` independently verified all16runtime/36technical/23failed/17operational pins and gives PASS with0Critical/Important/Minor for original criterion4. [The separate peer review](https://github.com/lumenpearson/cdpiui/issues/14#issuecomment-6816247958) also accepts the complete bounded batch with0blocking findings.

This qualifies the stated bounded G2 host and real-close composition. It does not prove real picker interaction, full visual startup, active-modal restoration, installed-product, G1 reconciliation or a release. Exact unchanged picker endpoints plus controlled-response actual-host checks justify source-preservation coverage within G2; issue15 retains the broader real-picker matrix.

## Original acceptance mapping and delivery boundary

| Original issue14 criterion | Pre-merge evidence |
|---|---|
| Document state/collection/subscription/cancellation/lifecycle ownership | Current owner records and source composition; whole-change review and current report |
| Preserve public/stored/pipe/order/identity/reentry contracts | Current-main source and compiled comparison, preserved paths, focused/full managed tests and bounded native owner checks; no full ABI claim |
| One implementation; focused/full tests pass | Whole-source review0 findings at e417;1845 focused/2590 full, all747 baseline test names retained |
| Changed native, cancellation, file identity and orderly shutdown | Separately authorized R2 host178 plus actual-close6-event tail; source-only justification for unchanged real picker endpoints; larger integrated matrix remains issue15 |
| Reviewed publication, final-head CI and actual integration | Aggregate PR233; final report review and exact final-HEAD check readback required immediately before merge; integrated SHA and original PR dispositions belong to the issue's subsequent acceptance record |

This report does not close the issue or imply a merge. After final independent review and successful required checks on the final PR HEAD, recheck unchanged main compatibility and integrate normally. Read back main reachability, original PR states and post-integration checks. The six original branch refs and later230/232 bases stay fixed; an original PR whose base was not advanced may retain an unmerged state even when its head is contained in main. Do not label a closed unmerged PR as delivered. Issue14 closes completed and actual Project7 becomes Done only when the six-section acceptance record verifies every original criterion. Original checklist copies describe the same five obligations. Issues13/15 and later work retain their own contracts.

Raw artifacts are retained in the owner-controlled common Git evidence directory `task-policy-rollout-2026-11-10/issue14-stack-through227/`: original/current API snapshots, source/tree/XML proofs, unique logs/TRX, compiled inventories/hashes, Project169/175 JSON/JPG, native source/build/resource/pin manifests, R1 correction snapshots, offline controls and the blocked prelaunch package. Evidence was not copied into another session's outputs.
