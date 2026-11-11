# Store Item Download Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline, task-by-task, with a fresh final independent reviewer.

**Goal:** Give the eleven download values, plain error latch and seven display methods one focused owner.

**Architecture:** Internal ItemDownloadPresentation stores Text without invocation and preserves
original field/event/localization order. The host keeps public/private forwards and one synchronous
sender bridge. Queue and operating workflows retain their original current reads.

**Tech Stack:** C#/.NET 8.0.427, xUnit linked source, WinUI3, exact source/public inventories,
immutable native corpus and explicit exclusive UI queue.

**Spec:** [download presentation design](../specs/2026-11-11-store-download-presentation-design.md).

## Global constraints

- Issue #225 actual Project intake 137/release 138 is verified before implementation.
- Use dev/store-download-presentation from frozen PR #224/12ef689; retain all prior refs/workspaces.
- Preserve eleven defaults/setters, plain error latch, seven private forwards and one host bridge.
- StatusTip always notifies even equal/null. Other properties use SetProperty equality.
- Error latch is checked once at guarded entry and written LAST after four error publications.
- Keep all stage/text/threshold/culture/partial exception/reentry behavior; no snapshots or new guards.
- Existing product changes are restricted to main ItemViewPageViewModel. Keep Queue.cs byte-identical,
  including speed formatting before the guard, operation identity, subscriptions and dispatcher.
- Keep two late CheckCurrentStatus reads, completion current-ID/error reads and all operating routes.
- No Load, Execute, queue delivery, StoreHelper, DB, install/delete, new operating cuts/IO or deletion.
- Every native launch and UI API needs a fresh explicit lease, independent READY and full postchecks.

## Review Focus

- StatusTip's unconditional assignment/notification differs from the other ten observable setters.
- ShowDownloadState and OnInstallingError have ordered publications with callback-visible partial state.
- A nested error may change the latch after a guarded entry; the outer call does not recheck it.
  Later outer error notifications see that current nested latch, including before a later outer failure.
- ApplyStage keeps its earlier header across callbacks and preserves branch-specific values it does not write.
- Time formatting observes culture after localization and preserves the earlier TimeSpan argument.
- New owner construction stays cold; prior host initialization and commands retain relative order.
- Bounded constructor/private-model evidence is distinct from Page loading, image visuals and queue delivery.

### Task 1: Extract the focused state and literal policy

**Files:** create CDPIUI/ViewModels/Store/ItemDownloadPresentation.cs; modify only the existing
main ItemViewPageViewModel.cs in product; add Presentation/ItemDownloadPresentationTests.cs and
one linked-source entry; focused design/plan/report and additive document inventory.

**Interfaces:** consumes a stored Text function and original method arguments. Produces eleven
current observable values, plain ErrorHappens and seven methods through host property/private forwards.

- [x] Read issue/spec/intake, preserve normal/source/compiled baselines, and obtain a fresh builder
  verdict before product code. Inspect accepted 172 cases for moved-field dependencies.
- [x] Write cold/ordering/gate/stage/culture/reentry/failure tests first and observe expected missing-owner RED.
- [x] Implement owner, eleven host forwards, seven method forwards, two remaining latch accesses and
  one cold constructor bridge; preserve prior statements. Run focused GREEN and inspect assertions.
- [x] Apply a controlled early error latch before error publications, observe behavioral failures,
  restore exact source/hash in finally, then focused/full GREEN and normal GUI with zero warnings/errors.
- [x] Verify source/compiled public preservation, exact existing footprint, whole-host/policy/property/
  constructor/bridge/latch/Queue guards, line bounds, governance and redacted scans; commit checkpoint.

### Task 2: Qualify actual host composition and accept locally

**Files:** fresh immutable F:/cdpiui-verify fixture, ignored source/build/pin/run/postcheck evidence,
dedicated report and additive inventory.

**Interfaces:** consumes Task 1 product pin and accepted 172 corpus, 34 old case partials and 21 cut
paths. Produces bounded real Store host/private setter/forward evidence and local draft acceptance.

- [x] Read Task 1 evidence/brief; preserve all accepted labels/case bytes/cuts and derive fresh counts.
  Add discriminating actual host/setter/guard/latch/reentry/failure cases before build.
- [x] Obtain independent pre-build review; build zero warnings/errors, pin all actual artifacts/resources/
  sources/runner/verifier and baseline filenames/hashes, then independent prelaunch READY.
- [x] Obtain a NEW explicit exclusive lease, run one exact owned process, and verify corpus/COMPLETE/
  exit/streams/source/resources/hash/IO/absence/process cleanup; idle/requestfalse and guarded release.
- [ ] Update evidence and obtain fresh whole-branch review. Resolve Critical/Important through observed
  RED to GREEN, retain failures, and ledger all rulings/deferred Minor findings and their costs.
- [ ] Publish a draft stacked on dev/blockcheck-wizard-navigation with Refs #14 and Refs #225; verify
  exact final-head checks and actual PR metadata/Project fields, without parent/main merge.
- [ ] Verify six criteria, post six-section LOCAL acceptance, immediately close completed/set actual
  Project Done and read back criteria, closure reason, labels, owner, milestone and fields.
