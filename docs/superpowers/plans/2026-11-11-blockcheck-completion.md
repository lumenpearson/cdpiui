# BlockCheck2 Completion Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline, task-by-task. Obtain a fresh final independent review.

**Goal:** Extract completion notice policy from the MainPage host without changing observable behavior.

**Architecture:** A small internal pure presenter reads lazy ports at the original branch points.
The host retains navigation, notice-open and severity/title/message publication. No rule duplication.

**Tech Stack:** C#/.NET8.0.427, xUnit linked source, WinUI3, Git/blob/API and immutable native evidence.

**Spec:** [completion design](../specs/2026-11-11-blockcheck-completion-design.md).

## Global constraints

- Issue217 actual intake99 is verified; root owns only the named new stage on its isolated branch.
- Preserve frozen PR2163623584, RootEebc/peer249 refs and all historical source/runtime evidence.
- Preserve all public signatures/setters, localization keys, lazy reads, notification sender/order,
  nullable raw values, exception identity and reentry. No new package, framework or behavior.
- No real settings/backend/history/network/scan/installation; native work requires reviewed cuts
  and a new explicit exclusive UI grant including snapshots/reads. Keep other chats paused.
- Retain evidence/workspaces despite skill cleanup defaults: the user forbids destructive deletion.
- Do not merge main or claim G1/full-native/integrated acceptance;13/14/15 stay separate.

## Review Focus

- View navigation and IsResultNoticeOpen callbacks can change state before completion policy starts.
- Repeated State/OperationError/HistorySaveError reads can differ; caches change existing semantics.
- Localization callbacks can change HTTP-only/error/history data before the next original read.
- Publication callbacks can throw or reenter; preserve partial state, sender and outer continuation.
- New native cases must exercise actual MainPage wiring, with old149 cases/cuts correctly attributed.

### Task 1: Extract and qualify the completion policy

**Files:** new ViewModels/BlockCheck2/BlockCheck2RunCompletionPresenter.cs; MainPageViewModel.cs;
MainPageViewModel.Progress.cs; Tests/Presentation/BlockCheck2RunCompletionPresenterTests.cs;
Tests.csproj; this plan/design and additive docs/governance/document-inventory.json.

**Interfaces:** consumes the eight lazy ports specified in the design; produces Show():void,
with one notice publisher call and no mutable invocation state. Host forwards through its original
private HandleRunCompleted and three setters.

- [x] Read the design/issue and actual metadata; preserve clean branch/base and API/normal GUI baseline.
  Check design with a fresh builder reviewer before implementation.
- [x] Write focused tests first for state/read/key/short-circuit/null/reentry/exception contracts.
  Observe the expected missing component or behavior RED before implementing it.
- [x] Implement the original policy in the presenter. Keep host lifecycle statements in place and
  add constructor wiring without invoking ports. Run focused tests and inspect actual output.
- [x] Prove a controlled cache/read mutation fails behavior assertions, preserve output, restore
  exact bytes/hash and run focused/full GREEN plus normal Release/x64 GUI0warnings/errors.
- [x] Verify source and compiled public API preservation, exact product footprint and current
  governance/secret scans. Record failures/resolution and commit the owned product checkpoint.

### Task 2: Qualify actual MainPage composition and accept the local stage

**Files:** new source-pinned F:/cdpiui-verify fixture and retained ignored stage workspace;
dedicated verification report and additive document inventory. Product stays at Task1 checkpoint.

**Interfaces:** consumes Task1 SHA and accepted union149 source/cases; produces a reviewed fresh
native corpus, actual build/pins and execution/postcheck receipt for this source only.

- [x] Read Task1 evidence; derive full Git source inventory, literal cuts, unchanged case partials,
  new actual-host cases and exact sequence/count. Preserve fresh paths and all prior fixtures.
- [x] Independently review isolation/new cases before build, compile0-0, then pin real binaries,
  resources, source, runner/verifier and all preexisting output names/hashes. Obtain READY.
- [x] Coordinate a new explicit exclusive lease, run the owned bounded process once, validate
  exact pass labels/COMPLETE/exit0/streams/IO/hash/resource/source/absence/process checks, then
  publish idle/requestfalse and guarded release. Preserve and diagnose failed attempts.
- [ ] Update evidence/docs; obtain fresh whole-change independent review, fix Critical/Important
  with observed RED→GREEN and ledger any rulings/deferred minors.
- [ ] Publish only this stage as a stacked draft PR against frozen dev/audit-class-integration;
  verify all required checks on its exact final HEAD and actual PR metadata/Project fields.
  Refs14 and217 keep integration ownership explicit; no automatic closure, parent draft or main merge.
- [ ] Check all six local issue criteria against named current evidence, post six-section acceptance,
  close completed+actual ProjectDone immediately and read back full metadata. Leave13/14/15 open.
