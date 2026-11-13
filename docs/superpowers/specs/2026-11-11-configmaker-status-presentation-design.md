# ConfigMaker Status Presentation Design

## Intent and accepted scope

Issue #228 continues the authorized audit decomposition after the accepted local Store stage #225.
The baseline is frozen draft PR #227 at `3afa82e3f4947ce4bef38b14d86c642c93ddbe90`.
ConfigMakerViewModel has 986 physical lines in nine partial files; its main file has 381.
Its status pipeline is a distinct responsibility among editor, document, help and test composition.
This task accepts a LOCAL presentation result. Parent #14 retains broader integration; #13/#15,
the earlier draft stack, installed-product acceptance and release keep their existing boundaries.

Execution owner: Root, chat `01a1bc96-ea11-73a1-a4a1-5cd6db1617fa`, in the existing isolated
`audit-class-decomposition/cdpiui` worktree, branch `dev/configmaker-status-presentation`.
Actual issue intake 149 confirmed In progress/P2/Architecture/Low/G2/lumenpearson and exactly
type:refactor plus area:architecture. A fresh browser navigation verified persisted fields after
the original reload left a blank body. Both owned tabs closed; guarded release 150 follows 149.

## Ownership and ports

Add internal sealed `ConfigMakerStatusPresentation<TSeverity>` in the existing ConfigMaker folder.
It owns only StatusTitle, StatusMessage, StatusSeverity, IsStatusOpen and their display sequence.
Its constructor accepts these exact ports, stores them and invokes or validates none:

```csharp
ConfigMakerStatusPresentation(
    TSeverity initialSeverity,
    Func<TSeverity, string?> resolveTitle,
    Action<TSeverity, string?, string?> raiseNotice,
    Func<bool> getInline)
```

Title and message start at string.Empty, severity uses the explicit initial argument, and open
starts false. All four public owner setters use the existing ObservableObject.SetProperty equality.
The focused owner stays below 160 lines. Public owner methods are `Show(string? message,
TSeverity severity)` and the four property setters; no alternate state or cached notification exists.
Use nullable annotations on this internal owner to describe the existing raw-string behavior.

The host composes the closed InfoBarSeverity owner after its two existing view/dispatcher guards,
before TextChangeRelay construction. Pass InfoBarSeverity.Informational explicitly. The title port
contains the original literal severity switch: Error uses SomethingWentWrong, Warning uses Attention,
and every other value uses Information. Each branch calls the current host localizer when invoked.
The notice port dynamically evaluates the original StatusNotificationRequested optional invocation
and constructs the existing StatusNotificationRequestedEventArgs with this VM as sender. The inline
port reads the current host UseInlineStatusMessages auto-property. Subscribe one synchronous owner
PropertyChanged bridge that calls the host OnPropertyChanged with the same name and host sender.
All previous constructor statements retain their relative order. New composition stays cold.

Keep public ShowEditorMessage as a forward. Keep StatusTitle, StatusMessage and StatusSeverity
public getters/private setters, and IsStatusOpen's public getter/setter, as owner forwards.
UseInlineStatusMessages remains the same host auto-property with default true. Keep the existing
UserControl's separate notification bridge unchanged; it intentionally changes sender to the control.
No other consumer, interface or public signature changes.

## Exact observable sequence

Show first obtains a local title from resolveTitle(severity), then invokes raiseNotice with the raw
severity/title/message, then reads getInline exactly once. When false, assign only IsStatusOpen=false
and return, retaining the other three current values. When true, assign StatusTitle, StatusMessage,
StatusSeverity and IsStatusOpen=true in that order, with each original equality decision at its point.

Title resolution can change the current event subscription before the notice. The notice can change
the inline flag or invoke ShowEditorMessage recursively. If the single later flag read returns true,
property callbacks that then change that flag do not stop the outer sequence. A nested call can change
current values; the outer call continues with its own earlier title/message/severity and compares
each assignment against those current nested values. Do not snapshot old state, subscribers or flag.

The actual EventArgs constructor normalizes null title/message to string.Empty. The owner receives
and stores raw null strings. Never normalize in the owner, reconstruct inline values from EventArgs,
or replace that existing constructor. Unknown InfoBarSeverity values use Information for the title
and retain their raw enum value in the event and inline severity. Add no enum constraint or mapping.

All callback exceptions propagate with their identity, without wrapping, rollback, catches, finally
or extra guards. A title failure prevents notice/flag/assignments; a notice failure prevents flag and
assignments; a flag-port failure prevents assignments. Each property observer sees its new value,
and a failure stops later assignments while retaining earlier and nested effects. Retry uses that
current partial state. The production inline auto-property does not itself call arbitrary code;
managed port-fault tests establish owner ordering, without claiming a native getter failure.

## Existing product boundary

Only main ConfigMakerViewModel.cs changes, beside the new internal owner. Remove its four old status
backing fields, replace the four property bodies and ShowEditorMessage, and add the cold composition
and bridge. The whole original host must reconstruct under exactly those declared substitutions.
Other partials, including direct ShowHelpLoadStatus closure through IsStatusOpen, remain byte-identical.
Keep all UserControl and EventArgs source, XAML, resources, packages, background/settings, logging,
document/help/test lifecycles, timer, IO, queue, Core/AddOns/Shared/security and other worktrees unchanged.
No static root, platform stub, new framework, synchronization or lifetime change is part of this task.

## Verification and delivery

Write linked-source tests first and observe the missing-owner RED. Use an owned test enum with a
nonzero initial severity to detect accidental default(TSeverity). Trace raw null/unknown values,
equality, cold ports, localization before dynamic notice lookup, one late flag read, enabled/disabled
paths, nested notice/property calls, outer locals, each port/property failure, retry and instances.
Hoist the inline read before notice in a controlled mutant; require real ordering/state failures,
restore exact source bytes/hash in finally, then focused/full GREEN and a normal Release/x64 GUI build.
Check whole-host reconstruction, literal severity/notice adapters, defaults/properties/constructor/
bridge/footprint and unchanged partials. Compare baseline 451 exported source types and compiled
452 GUI/88 Core shapes within their existing limited inventories. Run governance and redacted scans.

Create a NEW immutable native fixture from accepted Store178 R2, retaining all 178 labels, old case
bytes and 21 cuts. Derive actual counts. Add six unique ConfigMaker groups for actual cold host/owner,
public/private setters and sender, real severity/localizer/EventArgs values, late flag/disabled carryover,
nested notice/property/current equality, observer failures/retry and independent instances/raw values.
Use the current UI dispatcher and a view that rejects operating endpoints. Keep model/view cold;
do not call Load/OnViewLoaded, help, document/test commands, file operations or add operating cuts/IO.
Native raw-null caller evidence distinguishes EventArgs.Message empty from inline StatusMessage null.
Native title-port null, mutated subscriptions during localization, and arbitrary flag-port failures
remain managed/source-only where the uncut actual localizer/auto-property cannot produce them safely.
The UserControl's second sender bridge is unchanged source, without new loaded-control evidence.

Require independent pre-build/prelaunch READY, actual source/resource/artifact/runner/verifier pins,
a new exclusive UI grant and complete corpus/exit/streams/hash/resources/output/process/idle-release
checks. Obtain a fresh whole-range review, publish a draft stacked on frozen PR #227, verify final-head
CI and actual PR Project fields, then six-criterion local acceptance and immediate completed/Done/readback.
Full Page/UserControl, visual EN/RU/accessibility, operating workflows, GC collection, unsupported
concurrency, all metadata/native ABI, installed product, G1, integration and release remain separate.
Retain every prior ref/worktree/archive/failed or accepted artifact. Recover with additive commits.

## Executor choices

The three cold callbacks and typed internal owner are the executor's choice. They isolate a complete
pipeline for portable tests while keeping the existing platform adapters and their observation points.
Four state values alone would leave the critical ordered pipeline in the host; a platform-dependent
owner would require a test stub or native-only characterization. No user design approval is inferred:
the user explicitly authorized autonomous decomposition and opted out of confirmation questions.
Fresh builder review is required before product code. Dates and estimates remain unset.
