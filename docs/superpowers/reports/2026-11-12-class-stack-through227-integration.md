# Stack through PR 227: current integration verification

Owner: chat `01a1c5f4-bad8-7310-bafb-1bcf792172a9`, issue [14](https://github.com/lumenpearson/cdpiui/issues/14), branch `dev/class-stack-integration`, isolated `C:/Users/lumen/.codex/worktrees/future-task-issues/cdpiui`.

[Draft PR 233](https://github.com/lumenpearson/cdpiui/pull/233) is the current integration vehicle. **Issue 14 remains open; native acceptance and actual main integration are incomplete.** This report records current source, managed, compiled-contract and preparation evidence. It does not accept the whole issue.

## Delivery decision and preserved history

The maintainer explicitly requested integration of the stack through PR 227. This supersedes the prior executor-defined local/draft-only publication boundary in the historical 2026-11-11 class-integration plan, design and source report. Those records retain their original pins, results and limits. [The additive issue decision and independent boundary review](https://github.com/lumenpearson/cdpiui/issues/14#issuecomment-6806815231) explain the current boundary.

Issue 13 and PR 10 retain historical G1 reconciliation. The one older GUI optional-default difference and three Core expected-hash signature differences predate this integration. Current-main preservation is required; it is not G1 acceptance or adoption of that older branch. The delivery gates permit G2 alongside G1. Issue 15 retains broader integrated native/real-picker/startup scenarios and does not close with this source stage.

## Candidate and provenance

- Current main: `21555a29ce9fba98c678c445d04ee9f6ce3c85ba`.
- Plan-only first parent: `9c24c8a393ce91799a377dc96f7a3020aca8ed52`.
- Normal merge/product candidate: `e417d4efef05c47393bde549c301cb519577d9bc`.
- Stack tip: `3afa82e3f4947ce4bef38b14d86c642c93ddbe90`.

All six published heads are retained as ancestors: PR 216 `36235846fb116acad49b328b29120afa92714712`, PR 218 `3da11cb098d3e1241b2a86cd86ee0c14d80b1942`, PR 220 `3c7f8961477dbff0b4b021493b629ba1a1a0fa05`, PR 222 `b627a0f62dace8dde20ca2574a9d28646f71b421`, PR 224 `12ef689e0968bd7ff4b27cee0fe7af02091286da` and PR 227 at the tip above. Their published refs were preserved. Later PR 230/231 work and foreign checkouts were not merged, retargeted or modified.

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

## Prepared native checks: no current execution

Two fresh archives of e417 contain 1606 tracked inputs each. Host has 38 declared extras, shutdown 39. Each has exactly 21 reviewed boundary files. All original cut-file inputs match the historical accepted source. The 36 accepted bootstrap files are retained; the main has exactly three declared identity changes: own root, current PIN and log basename.

The fresh host preserves the canonical 178 groups. The shutdown fixture uses two plain no-content owned Window types, actual App registration and CloseWindow/Closed delivery, uncut close-handler/coordinator/cleanup/Low dispatcher/idle forwarding, and an observational wrapper that records state before calling real Application.Exit. It requires a first-close Low FIFO barrier, retained sentinel identity, final empty original registry and exactly one validated real-exit marker. It has no successful unconditional exit fallback. Theme/titlebar behavior and active modal restoration are outside this tail.

The source review caught two Important fixture issues before compilation: old root/PIN literals and a no-longer-isolated theme/logger path caused by Grid content. Both were corrected; previous source/manifests are retained. A preparation-script quoting error stopped before file writes and was corrected. These are fixture preparation records, not product regressions.

Builds: host had 0 warnings/errors in 74.59 seconds; shutdown had 0 warnings/errors in 59.45 seconds. Post-build source hashes and input sets match. Each artifact baseline has 522 files, embedded asInvoker/uiAccess=false, 20 packaged resource dictionaries matching generated bytes and 5206 values matching source JSON. Five offline tail-parser negative controls reject missing, duplicate or premature exit evidence, incomplete cleanup and failure mixed with success. The positive control passes. Optimized Python is rejected.

The native helper requires a fresh direct human native-resume record, a matching new coordinator grant, all other UI participants paused and the reviewed bundle hash. It writes no queue-owner file. The native user hold is still blocked. A run-mode negative control was rejected before preflight or launch. Read-only preflight passes for both packages. **Native application launches in this case: zero.** Final prelaunch review remains pending.

Exact unchanged ConfigMaker/BlockCheck/CreateConfig picker endpoints and view interfaces, combined with controlled-response host tests, justify G2 source-preservation coverage of unchanged picker implementations. They do not prove real picker Cancel. Issue 15 retains real picker, second selection, missing-resource replacement and full integrated scenarios.

## Original acceptance and next evidence

| Original issue 14 criterion | Current assessment |
|---|---|
| Document state/collection/subscription/cancellation/lifecycle ownership | Supported by current source and ownership records; this report needs final delta review |
| Preserve public/stored/pipe/order/identity/reentry contracts | Supported within source, tests and bounded metadata checks; no full ABI claim |
| One implementation; focused/full tests pass | Verified on e417 within the reviewed scope |
| Changed native, cancellation, file identity and orderly shutdown | Incomplete: fresh178 and real close-tail execution/postchecks required |
| Reviewed publication, final-head CI and actual integration | Draft published; e417 source/CI verified; final docs/native/main integration remain |

No issue checklist is declared fully accepted by this report. After approved native execution, record all results and limits, review the final report delta and final PR HEAD checks, integrate normally, and read back the resulting main revision and original PR dispositions. Close issue14 completed and set actual Done immediately only when every original criterion passes. Keep13/15 and later local-stage goals separate.

Raw artifacts are retained in the owner-controlled common Git evidence directory `task-policy-rollout-2026-11-10/issue14-stack-through227/`: original/current API snapshots, source/tree/XML proofs, unique logs/TRX, compiled inventories/hashes, Project169/175 JSON/JPG, native source/build/resource/pin manifests, R1 correction snapshots, offline controls and the blocked prelaunch package. Evidence was not copied into another session's outputs.
