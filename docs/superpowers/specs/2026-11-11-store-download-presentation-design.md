# Store item download presentation ownership

## Intent and delivery boundary

[Issue #225](https://github.com/lumenpearson/cdpiui/issues/225) owns this LOCAL continuation of
the audit decomposition. Frozen baseline: draft PR #224 at
`12ef689e0968bd7ff4b27cee0fe7af02091286da`. Its local stage #223 is completed with six verified
criteria, actual Project Done under grant 135 and
[final readback](https://github.com/lumenpearson/cdpiui/issues/223#issuecomment-6798650935).
The main ItemViewPageViewModel source has 562 lines; Queue.cs has 122, total 684.

Root owns `dev/store-download-presentation` in the existing isolated worktree. Actual issue
Project intake 137 persisted In progress/P2/Architecture/Medium/G2/lumenpearson and two canonical
labels after reload. Its [receipt](https://github.com/lumenpearson/cdpiui/issues/225#issuecomment-6798817588)
and guarded release 138 precede implementation. The parallel utility owner confirmed no overlap.
No parent/main merge, operating Store action, installation or deletion belongs to this stage.

## Chosen owner

Create one internal `ItemDownloadPresentation : ObservableObject` in the Store view-model folder.
It owns eleven existing display values and the plain error latch. One stored `Func<string, string>`
performs localization lazily. Its constructor only stores that function; it invokes no callback.
No source interface or general framework is needed because policy reads only its own current state.

| Property | Cold value | Notification rule |
| --- | --- | --- |
| IsActionVisible | true | SetProperty equality |
| IsDownloadStatusVisible | false | SetProperty equality |
| IsErrorStatusVisible | false | SetProperty equality |
| IsStopEnabled | true | SetProperty equality |
| StatusText | string.Empty | SetProperty equality |
| IsSpeedVisible | false | SetProperty equality |
| SpeedText | string.Empty | SetProperty equality |
| StatusTip | null | Assign and notify on every call, including equal/null |
| IsProgressIndeterminate | true | SetProperty equality |
| ProgressValue | 0d | SetProperty equality, including double NaN semantics |
| ErrorName | ERR_FLASHLIGHT_BISY | SetProperty equality; preserve this exact identifier |
| ErrorHappens | false | Plain assignment, no model property event |

The eleven observable properties are writable on the internal owner. Keep their public host getters
and private setters as forwards. ErrorHappens has a plain getter/setter on the internal owner so
the original Install reset and completion-route read occur at the same points, without a new event.

The host adds one readonly field. After the original view and dispatcher assignments, construct
the cold owner with `key => _localizer.GetLocalizedString(key)`, then attach one synchronous bridge
to `OnPropertyChanged(args.PropertyName)`. Keep all prior constructor statements in relative order,
including the three RelayCommand objects. Do not move the packaged BitmapImage initializer,
localizer initialization or AdditionalInfo collection.

The aggregate reference graph belongs to the host. Add no static event, loaded subscription,
disposal requirement, thread guard or reentry lock. Source inspection can show ownership; actual
GC collection remains outside this qualification.

## Seven policy methods and observable sequence

Keep all seven private host methods as direct forwards with their existing argument types:
ShowDownloadState, ApplyStage, OnStageChanged, OnProgressChanged, OnTimeRemainingChanged,
OnSpeedChanged and OnInstallingError. The owner holds their original bodies, with only localization
and owned field/property references changed. Preserve raw nullable strings and double values.

ShowDownloadState writes Stop enabled, indeterminate, Error hidden, Action hidden and Download
visible in that order. Each publication can invoke a callback before the next value is evaluated.

ApplyStage maps null to QueueWaiting and keeps a local header string. GETR, END, Extracting,
Completed and CANC localize the original key, then set indeterminate. Downloading localizes,
sets determinate and then shows speed. ErrorHappens only selects its header; it does not change
indeterminate, speed or the error latch. ConnectingToService localizes its header, localizes and
publishes StatusTip, then sets indeterminate. Unknown and empty stage strings are localization keys
verbatim. Every branch publishes the stored header to StatusText last. No other state is reset.

OnStageChanged reads ErrorHappens once and returns if true. Otherwise it calls ShowDownloadState,
hides speed and calls ApplyStage, without checking the latch again after callbacks. OnProgressChanged
also checks once, then sets determinate before the raw progress value. OnSpeedChanged checks once
before assigning the raw speed string.

OnTimeRemainingChanged checks the latch once. At one minute or more, localize LeftMinutes before
formatting the truncated integer minute count. Current culture is observed by string.Format after
localization. Below the threshold, localize LeftSmall. Assign StatusTip through its unconditional
setter. Keep the earlier TimeSpan argument across callbacks and preserve formatting failures.

OnInstallingError has no early guard. This invocation shows Error, hides Action, hides Download
and assigns ErrorName before its own final latch assignment. Notifications observe the current
latch, including changes made by nested calls. A nested error can set it true before the outer
ErrorName notification; later guarded calls respect that current value. An exception prevents the
remaining outer statements and preserves all effects already performed by either frame. Retry
follows ordinary equality behavior; do not snapshot or roll back the latch.

Preserve exception identity, nested outer/inner frames, field-before-event behavior and partial
effects. An early latch, equality optimization for StatusTip, new validation or a late recheck would
change behavior and needs a separate requirement.

## Host and operating boundaries

Only the existing main ItemViewPageViewModel source changes. Queue.cs remains byte-identical,
including operation-ID lookup, handler versions, dispatcher checks, callback identity and speed
formatting with its literal comma-space before OnSpeedChanged's error gate.

CheckCurrentStatus keeps two separate Store queue-record reads; Progress is read after ApplyStage
callbacks. ShowItemAfterInstallActions keeps its initial ID gate, ordered resets and later current
ID/error checks. Load, Install, Delete, support checks, StoreHelper, DB, licenses and all other
operating actions remain in the host. Only remaining plain latch accesses become owner accesses.
Do not capture item, ID, queue record, error or supported state at a new point.

Keep public/compiled signatures, XAML, resources, packages, Core/AddOns/Shared/security and other
sessions' worktrees unchanged. Target the main source below 500 lines and the focused owner below
300; these counts supplement the responsibility boundary and do not justify unrelated extraction.

## Verification and recovery

Write focused tests before implementation and observe a missing-owner RED. Cover all cold values,
eleven setters, unconditional StatusTip, error gates, every stage, threshold/culture, raw null/NaN,
callback mutation, nested frames, exception identity, partial effects, retry and instance isolation.
An early ErrorHappens assignment must fail real assertions. Restore exact source in finally, verify
its hash and pass focused/full tests and the normal GUI build. Add a discriminating nested-error test:
from the first outer error notification, invoke an inner error once. The later outer ErrorName
notification must observe latch true, its final value must use the outer argument, and a new guarded
call must respect the nested latch. Also retain nested effects if a later outer observer throws.

Compare the complete old host under declared substitutions and all seven literal policy bodies,
eleven property forwards, constructor order, cold dependencies, bridge and plain latch. Preserve
Queue.cs and all other existing product bytes. Compare source signatures for the 451 baseline
exported types and compiled 452 GUI/88 Core type shapes, with explicit inventory limits.

Create a fresh immutable source-pinned fixture from accepted native 172. Retain all 34 case partials,
labels and 21 cut paths. Add actual cold ItemViewPageViewModel/private-forward/setter cases with
the current dispatcher and a four-method view substitute that throws on every operating endpoint.
The existing constructor's BitmapImage evidence is limited to its owned packaged URI. Do not call
Load, Execute, queue delivery, StoreHelper, install/delete/DB or add operating cuts/IO. Require
independent pre-build and prelaunch checks, fresh pins, an explicit new exclusive lease and complete
source/resource/corpus/output/hash/process/idle-release postchecks. Derive every count from inputs.

Full Page/XAML, image decoding/visuals, real queue delivery, unchanged operating routes, installed
product, runtime GC collection, G1, integration and release remain separate. Keep both navigation
native attempts and all new accepted or rejected artifacts. Recover through an additive corrective
commit. Refs #14 and Refs #225.
