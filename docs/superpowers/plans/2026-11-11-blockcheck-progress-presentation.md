# MainPage Progress Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline, task-by-task, with a fresh final independent reviewer.

**Goal:** Move six progress display values and their update policy into one focused state owner.

**Architecture:** An internal observable owner stores the same fields and lazy read ports. Public
host properties/private entrypoints forward to it; one synchronous bridge retains host notifications.

**Tech Stack:** C#/.NET8.0.427, xUnit linked source, WinUI3, API/blob and immutable native evidence.

**Spec:** [progress presentation design](../specs/2026-11-11-blockcheck-progress-presentation-design.md).

## Global constraints

- Issue219 actual intake107 is verified before implementation; Root owns the isolated branch.
- Base/frozen PR2183da11cb and parent2163623584, peer249/f706 and RootEebc remain intact.
- Preserve all six cold values, public getters/private setters and five host method entrypoints.
- Read lazy ports and publish synchronously in the original order; no snapshots/guards/catches.
- Numeric progress/estimator167, duration formatter, lifecycle, completion217, resources and all
  backend/security/storage/package versions remain unchanged.
- No real preferences/backend/history/scan or OnLoaded; native testing needs independent READY
  and a NEW explicit exclusive UI lease, including all snapshots/reads.
- Retain source/evidence/workspaces; no deletion, reset, shared-main rewrite or unqualified merge.

## Review Focus

- Phase publication can change counts and terminal state before later displays read them.
- Stage publication can change running/phase/overall estimate; overall must read current values.
- Repeated equal updates still evaluate ports but suppress notifications under existing SetProperty.
- Nested disclosure callbacks change visibility before the outer localization and may throw.
- Host/owner event bridge must be synchronous, attached once and retain sender/partial state without
  adding loaded/unloaded or static lifetime behavior.

### Task 1: Extract the progress display owner

**Files:** new CDPIUI/ViewModels/BlockCheck2/BlockCheck2ProgressPresentation.cs; MainPageViewModel.cs;
MainPageViewModel.Progress.cs; CDPIUI.Tests/Presentation/BlockCheck2ProgressPresentationTests.cs;
test project link, this design/plan and additive document inventory.

**Interfaces:** consumes the eight lazy ports specified in the spec; produces six observable
properties and Toggle/Update/Format methods with the old names. MainPage preserves its public and
private forwarding entrypoints and one PropertyChanged bridge.

- [x] Read the issue/design and actual fields; preserve parent and current source/compiled/normal
  artifact baselines. Have a fresh builder check the design before code.
- [x] Write focused tests first and observe the expected missing-component/behavior RED.
- [x] Implement cold owner state, exact original policy and host facade/bridge, without touching
  lifecycle/estimator/duration/navigation/completion. Run focused tests and inspect real results.
- [x] Apply a meaningful controlled early-read/cache mutation, observe behavior assertions fail,
  restore exact bytes/hash and run focused/full GREEN plus normal Release/x64 GUI0-0.
- [x] Verify source/compiled public preservation, exact product footprint, source/bridge/read-order
  guards, governance and redacted secret scans. Record failures and commit the product checkpoint.

### Task 2: Qualify actual host composition and accept locally

**Files:** fresh F:/cdpiui-verify source-pinned fixture and retained ignored stage artifacts;
dedicated verification report and additive inventory.

**Interfaces:** consumes Task1 source pin and accepted154 source/cases/cuts; produces an independently
reviewed new actual-host corpus, pins/runtime/postchecks and local delivery record.

- [x] Read Task1 evidence; preserve154 prior labels/case bytes, derive all source inputs/cuts/extras
  and new actual host cases before compiling a fresh path.
- [x] Obtain independent pre-build review, build0-0, derive actual artifacts/resources/source/runner/
  verifier pins and all prior output hashes, then independent prelaunch READY.
- [x] Coordinate a new explicit lease, execute one owned process and prove labels/COMPLETE/exit0,
  streams/IO/source/resources/hashes/absence/process cleanup; idle/requestfalse/guarded release.
- [x] Update evidence/docs; obtain fresh whole-change review, resolve Critical/Important with
  observed RED→GREEN and ledger rulings/deferred minors. Preserve original artifacts.
- [ ] Publish a stacked draft against frozen dev/blockcheck-completion-presentation with Refs14/219;
  verify exact final-head checks and actual PR/Project fields, no parent or main merge.
- [ ] Verify all six local issue criteria, post the six-section acceptance record, immediately
  close completed/set actual ProjectDone and read back closure/classification/owner/fields.
