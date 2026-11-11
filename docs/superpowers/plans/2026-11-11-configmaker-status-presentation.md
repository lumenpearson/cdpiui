# ConfigMaker Status Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline, task-by-task, with a fresh final independent reviewer.

**Goal:** Give ConfigMaker's four status values and ordered message publication one focused owner.

**Architecture:** One internal ConfigMakerStatusPresentation<TSeverity> stores explicit initial severity
and three cold title/notice/inline ports. Host public properties/ShowEditorMessage forward, retaining
literal InfoBarSeverity/localizer/EventArgs adapters, one synchronous host bridge and the current flag.

**Tech Stack:** C#/.NET8.0.427, linked-source xUnit, WinUI3, bounded public inventories, immutable native
corpus and the shared exclusive UI queue.

**Spec:** [status presentation design](../specs/2026-11-11-configmaker-status-presentation-design.md).

## Global constraints

- Issue #228 actual intake149/release150 and accepted225/frozenPR227/3afa precede implementation.
- Use dev/configmaker-status-presentation in Root's existing isolated checkout; retain every old ref.
- Owner <160lines, exactly four values, explicit initial severity and three cold stored ports.
- No constructor invocation/validation; four original equality rules and public/private host accessors.
- Resolve title, dynamically raise host notice, read current inline flag ONCE, then ordered assignment.
- EventArgs normalizes null to empty; inline strings remain raw null. Unknown severity stays raw.
- Preserve callback sender/order, current nested equality, outer locals, exceptions, partial state/retry.
- Existing product edits only main ConfigMakerViewModel.cs; all other partials and operating source exact.
- No XAML/resources/packages/framework/platform stubs/IO/cuts/lifetime/security or other worktree change.
- Every native process and UI call needs owned current lease, reviewed pins and complete postchecks.

## Review Focus

- Localization can replace the event subscriber before notice: native real localizer, managed dynamic-port test and literal host adapter guard.
- A notice changes the flag before its one read; a later property callback changes it after that read without stopping the outer publication.
- Nested calls change current equality comparisons while the outer sequence retains its local arguments.
- Existing EventArgs null-to-empty differs from inline raw null; unknown severity uses the default title without enum validation.
- Every port/observer failure preserves identity and completed effects; native auto-property getter/title-null limitations stay explicit.
- Host construction remains cold, all previous composition order stays intact, and the UserControl's second sender bridge remains unchanged source.

### Task 1: Extract the owned pipeline with portable characterization

**Files:** create CDPIUI/ViewModels/ConfigMaker/ConfigMakerStatusPresentation.cs and
CDPIUI.Tests/Presentation/ConfigMakerStatusPresentationTests.cs; modify only existing product main
CDPIUI/ViewModels/ConfigMaker/ConfigMakerViewModel.cs and one CDPIUI.Tests.csproj source link;
dedicated design/plan/report plus additive document inventory.

**Interfaces:** constructor `(TSeverity initialSeverity, Func<TSeverity,string?> resolveTitle,
Action<TSeverity,string?,string?> raiseNotice, Func<bool> getInline)`; method
`Show(string? message, TSeverity severity)`; four owner properties with original names. Host consumes
InfoBarSeverity, preserves its current auto-property and original optional EventArgs publication.

- [ ] Read brief/spec/intake; save normal/source/compiled baselines and original host/partial hashes.
  Obtain fresh builder verdict before product code. Inspect accepted178 cases for old-field dependence.
- [ ] Write cold test first, run filtered test before owner/source link exists.
  Run: `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ConfigMakerStatusPresentationTests`.
  Expected: missing-owner CS0246, not a passing placeholder. Then write all behavior cases before implementation.
- [ ] Cover explicit nonzero initial severity; null stored callbacks cold; empty/default fields; all setter
  equal/null/raw severity rules; title/notice/one-flag/setter trace; disabled-open closure retaining other
  values; notice changes flag in both directions; title observer changes flag without stopping;
  localization swaps notice subscriber; nested notice/property/outer locals/current equality;
  each of three port and four property faults, identity/partial state/retry; repeated/independent models.
- [ ] Implement the specified owner and run the same focused command. Expected: all cases pass, zero warnings.
  Wire four host properties, public method, explicit initial severity/three cold adapters and one bridge;
  preserve all other host statements and partial bytes.
- [ ] Controlled mutant: read getInline before raiseNotice, use that value later. Run the focused command.
  Expected: meaningful notice/ordering/late-flag failures, no compile failure. Restore exact bytes/hash in finally.
  Rerun focused then full `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore`.
  Expected: all pass, no failures/skips, actual counts recorded.
- [ ] Run normal GUI `dotnet build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore` sequentially.
  Expected: zero warnings/errors. Verify source451/compiled452GUI88Core retained signatures/shapes,
  whole-host/literal adapters/four defaults/property accessors/bridge/cold constructor/partial/footprint
  guards, owner line bound, `python .github/scripts/check_governance.py`, whitespace and redacted scans.
  Expected: no drift/errors/out-of-scope change. Record failures and checkpoint product commit.

### Task 2: Qualify actual composition and close the local stage

**Files:** NEW source-pinned F:/cdpiui-verify fixture and archive, this plan's ignored build/pin/run/
postcheck evidence, dedicated verification report and additive document inventory.

**Interfaces:** consumes Task1 product pin and accepted Store178 R2, every old raw case and21cutpaths.
Produces actual ConfigMaker host/private property/public Show/EventArgs/localizer/current-dispatcher
evidence, six new groups, a stacked draft and qualified LOCAL acceptance. Derive every physical count.

- [ ] Read Task1 brief/evidence; retain old178 labels/cases/cuts. Add six actual-composition groups before
  build: cold defaults/accessors/closed owner; severity titles/unknown/null event versus field; notice
  flips flag/disabled-open carryover/direct setter; nested notice/property/outer arguments/current
  equality; each four observer boundaries and notice exception/identity/partial retry; independent
  instances/raw setters/equality. Use the actual cold model and a view that rejects operating endpoints.
- [ ] Independently review source/case/corpus/scope before build. Build the NEW fixture.
  Expected: zero warnings/errors. Pin actual binaries/resources/source/runner/verifier/full prior output
  filenames/hashes. Require independent prelaunch READY, no pending pins/process/output and actual PE scope.
- [ ] Request a NEW exclusive lease after all peers idle; run one exact owned process.
  Expected: exactly184uniquePASS=178+6, all prior prefixes, oneCOMPLETE/0FAIL/exit0/empty streams.
  Verify all pins/prior output hashes/resources/source/corpus, exact new names/golden reports/inputs/
  forbidden absence and normal GUI unchanged; exact owned process absence, idle/requestfalse/release.
- [ ] Update report; obtain fresh whole-branch independent review on the most capable available model.
  Expected: no unresolved Critical/Important. Resolve through observed RED to GREEN/full suite in one
  fix pass; ledger every ruling and deferred Minor. Retain all failed attempts/artifacts.
- [ ] Publish draft stacked on frozen dev/store-download-presentation/3afa with Refs #14 and Refs #228.
  Expected: exact final-head required checks success and actual PR metadata/Project readback, no merge.
- [ ] Match six criteria to evidence and post six-section LOCAL acceptance. Immediately completed/actual
  Done/readback all criteria and metadata. Expected: correct closure reason/labels/assignee/G2/Project
  fields, post-edit metadata success and parents13/14/15/earlier drafts open/unmerged.

## Limits and recovery

Native actual title-null/localization-subscriber mutation/arbitrary flag-getter faults are not promised;
portable tests and exact source adapters cover their respective boundaries. No loaded UserControl/Page,
visual/accessibility/EN-RU, operating workflow, GC/thread extension, full ABI, installed product/G1/
integration/release result is implied. Retain every ref and evidence file; use additive corrections.
