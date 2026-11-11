# MainPage Wizard Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline, task-by-task, with a fresh final independent reviewer.

**Goal:** Move wizard step, Next state and Cancel availability into one focused navigation owner.

**Architecture:** An internal observable owner reads a live source with seven bool properties,
calls the existing view and uses one command-notification action. The host keeps its bound API and
private forwarding entrypoints, with one synchronous property bridge.

**Tech Stack:** C#/.NET 8.0.427, xUnit linked source, WinUI3, exact API/source and immutable native evidence.

**Spec:** [wizard navigation design](../specs/2026-11-11-blockcheck-wizard-navigation-design.md).

## Global constraints

- Issue #223 actual Project intake under grant 127 is verified before implementation.
- Use isolated dev/blockcheck-wizard-navigation from frozen PR #222/b627a0f; preserve all parent refs.
- Preserve SiteLists/true/null/true defaults, two public Next properties/private setters, one command
  instance, and private GoToStep/SetCancelEnabled/UpdateNavigationButtons forwards.
- Preserve running/step observations before the view, current source reads after it and every original
  short circuit. SetCancelEnabled always assigns and notifies, including equal values.
- Construct the cold owner before CancelCommand; no source/view/text/command callback runs early.
- Change only the main existing MainPage file, plus mechanical step reads. Keep all other owners,
  async workflow bodies, models/interfaces, Session/options, resources, packages and operating code.
- No OnLoaded or command Execute in native cases; no new operating IO/cuts. Native launch needs
  independent READY and a NEW explicit exclusive lease, including read-only UI APIs.
- Retain every worktree/ref/archive/accepted and rejected artifact; no deletion, reset or parent merge.

## Review Focus

- View callbacks can change Session/current step after visibility and step flags are computed.
- Equal Cancel values still notify the same command; its observer reads the assigned value and can fail.
- Advanced uses validity then CanContinue, without a HasTargetSources guard; Mode and other steps
  retain their own short circuits, even when the Next button is hidden during running.
- Nested view/command/property/text calls preserve each frame's earlier flags and later current inputs.
- Constructor callback capture and one synchronous bridge retain coldness, sender and lifetime without
  a new static root, loaded subscription or notification for plain step/cancel state.

### Task 1: Extract navigation state and the direct live source

**Files:** create IBlockCheck2NavigationSource.cs, BlockCheck2NavigationSource.cs and
BlockCheck2WizardNavigation.cs beside MainPage; modify only MainPageViewModel.cs in existing product;
add CDPIUI.Tests/Presentation/BlockCheck2WizardNavigationTests.cs and links for owner/interface/
existing view/enum/record; design/plan/report and additive inventory.

**Interfaces:** consumes seven current source bools, existing two-method view, Text and unconditional
command notification. Produces current step/Cancel getters, two observable Next properties and the
three existing private host entrypoints through direct forwards.

- [ ] Read issue/spec/intake and retain source/compiled/normal baselines. Obtain a fresh builder verdict
  before product code. Verify no old native case depends on moved private fields.
- [ ] Write focused cold/state/ordering tests first and observe expected missing-component RED.
- [ ] Implement the exact source and owner policy, host forwards/current-step reads and one bridge;
  preserve prior constructor statement order. Run focused GREEN and inspect real assertions.
- [ ] Apply a controlled CanCancel cache before the view callback; observe behavior failures, restore
  exact bytes/hash and pass focused/full GREEN plus normal GUI with zero warnings/errors.
- [ ] Verify source/compiled public preservation, exact footprint, original policy/read points/seven
  mappings/host fields/constructor/command wiring, governance and redacted scans; commit checkpoint.

### Task 2: Qualify actual host/command composition and accept locally

**Files:** fresh immutable F:/cdpiui-verify fixture and ignored recipes/pins/logs/postchecks;
dedicated report and additive inventory.

**Interfaces:** consumes Task 1 source pin and accepted 166 corpus/case bytes/cut paths. Produces
actual host/source/view/command evidence and local delivery without accepting parent integration.

- [ ] Read Task 1 evidence/brief; preserve all 166 labels and old case bytes/cuts, derive fresh input,
  corpus/pin/extra counts and discriminating command/current-step callback cases before build.
- [ ] Obtain independent pre-build review, build zero warnings/errors, derive actual artifacts/resources/
  source/runner/verifier pins and full output baseline, then independent prelaunch READY.
- [ ] Acquire a new explicit exclusive lease; run one process and prove labels/COMPLETE/exit/streams,
  source/resources/hashes/IO/absence/process cleanup, idle/requestfalse and guarded release.
- [ ] Update evidence and obtain fresh whole-branch review; resolve Critical/Important through observed
  RED→GREEN, ledger all rulings/deferred minors and retain original artifacts.
- [ ] Publish a draft stacked on dev/blockcheck-review-presentation with Refs #14 and Refs #223;
  verify exact final-head checks and actual PR metadata/Project fields, without parent/main merge.
- [ ] Verify all six criteria, post six-section LOCAL acceptance, immediately close completed/set actual
  Project Done and read back closure, criteria, labels, owner, milestone and fields.
