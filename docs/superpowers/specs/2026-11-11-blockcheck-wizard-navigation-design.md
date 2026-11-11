# MainPage wizard navigation ownership

## Intent and execution boundary

[Issue #223](https://github.com/lumenpearson/cdpiui/issues/223) owns this LOCAL continuation of
the audit decomposition. Frozen baseline: draft PR #222 at
`b627a0f62dace8dde20ca2574a9d28646f71b421`. Its local stage #221 is completed with all six
criteria, actual Project Done under grant 125 and
[final readback](https://github.com/lumenpearson/cdpiui/issues/221#issuecomment-6796855871).
The four MainPage partials total 677 lines. This stage extracts its remaining navigation state
and projection without changing asynchronous workflow or operating behavior.

Root owns `dev/blockcheck-wizard-navigation` in the existing isolated worktree. Actual issue #223
Project intake under grant 127 was verified after reload: In progress/P2/Architecture/Medium/G2,
lumenpearson and exactly two canonical labels. The
[receipt](https://github.com/lumenpearson/cdpiui/issues/223#issuecomment-6797095818) records it.
Queue 128 is released with all six participants paused. No lease is inferred from that state.

These design choices are the executor's under the user's explicit autonomous authorization.
A fresh builder checks them before implementation. Keep parent issues #13/#14/#15 and draft PRs
#216/#218/#220/#222 open and separate; no main integration or release is accepted here.

## Structure and state

Create three internal files beside the existing MainPage owners:

| File | Responsibility |
| --- | --- |
| IBlockCheck2NavigationSource.cs | Seven read-only Session observations used by navigation. |
| BlockCheck2NavigationSource.cs | Direct live Session adapter, with no copied values or policy. |
| BlockCheck2WizardNavigation.cs | Current step, Next state, Cancel availability and original projection policy. |

The owner derives from ObservableObject. Keep these cold values:

| Value | Initial state | Notification rule |
| --- | --- | --- |
| CurrentStep | BlockCheck2WizardStep.SiteLists | Plain state; no PropertyChanged. |
| IsNextEnabled | true | Existing SetProperty equality and field-before-event semantics. |
| NextButtonText | null | Existing SetProperty equality and field-before-event semantics. |
| IsCancelEnabled | true | Plain state; command notification on every setter call. |

Expose CurrentStep and IsCancelEnabled as getters on the internal owner. IsNextEnabled and
NextButtonText are writable there so host private setters can forward without a second SetProperty.
Keep the host's public property signatures and the existing CancelCommand object.

Constructor dependencies are:

```csharp
BlockCheck2WizardNavigation(
    IBlockCheck2NavigationSource source,
    IBlockCheck2MainPageView view,
    Func<string, string> text,
    Action notifyCancelChanged)
```

All four dependencies are stored without invocation. Reuse the existing two-method view interface,
WizardStep enum and NavigationButtons record. The source has seven bool getters: IsRunning,
CanCancel, HasTargetSources, AdvancedOptionsValid, CanContinueFromTargets, CanStart and HasReport.
The first six directly return same-named Session properties; HasReport returns Session.LastReport
!= null at the original conditional read point. Its constructor stores Session without reading data.
Do not implement a new interface on Session or change existing model/view definitions.

In the host, add one readonly navigation owner and create it after the existing view/duration setup,
before CancelCommand construction. Supply a stored closure that calls the eventual existing
CancelCommand.NotifyCanExecuteChanged. Constructor coldness is essential: this closure must not run
before the command is assigned. The RelayCommand constructor itself only stores callbacks.
Its CanExecute closure reads navigation.IsCancelEnabled. Keep all prior constructor statements in
their relative order, and use the stored host view when creating the owner.

Attach one synchronous owner.PropertyChanged bridge to host.OnPropertyChanged(args.PropertyName).
It forwards only the two bound Next properties because plain step/cancel state raises no event.
No bridge is added by OnLoaded/OnUnloaded and no new static event or lifetime mechanism is needed.
The object graph belongs to the host and shares its lifetime; no GC collection claim is implied.

Remove the four old backing fields. Every remaining host step read becomes navigation.CurrentStep
at its original point. Keep private GoToStep(step), SetCancelEnabled(value) and
UpdateNavigationButtons as direct forwards. Async Next/Back/Cancel methods otherwise stay intact.
Keep notices, progress/review/completion owners, localization, Session/options/site lists, resources,
packages and operating code unchanged. Only the existing main MainPage file changes in product.

## Original sequencing contract

GoToStep always assigns the new step before calling view.GoToStep(step), including repeated or
unknown enum values. A view callback may call GoToStep again; the inner step remains current after
the outer view call returns. A thrown view exception retains the already assigned step. Add no
validation, equality guard, catch or automatic navigation refresh.

SetCancelEnabled always assigns its argument, then calls notifyCancelChanged, even for equal values.
Do not use SetProperty or notify a model property. The command event keeps the existing command as
sender, and CanExecute must observe the new value inside that event. An observer exception leaves
the assigned availability and stops the caller at the same point. There is no notification deduplication.

Move UpdateNavigationButtons literally with these declared source/Text substitutions:

1. Read source.IsRunning, then derive all six local step flags from the current stored step.
2. Construct the same NavigationButtons record and call view.ShowNavigationButtons. Exit follows
   WorkComplete; Back is !running; Cancel is running; Next is !running && !onProgress && !onComplete.
   ViewResult reads HasReport only when !running && onComplete. All these values precede the view
   callback and retain their original observations.
3. After that callback, read current source.CanCancel and call SetCancelEnabled unconditionally.
   Its command observer can change later inputs before Next evaluation.
4. Set IsNextEnabled using the original local step flags and lazy current source reads: SiteLists
   reads HasTargetSources; Advanced reads AdvancedOptionsValid then CanContinueFromTargets only
   when valid; Mode is true; Review reads CanStart; other steps are false. Do not add !running or
   HasTargetSources checks. In particular, CanContinueFromTargets is independent of HasTargetSources.
5. Localize StartButton for the original onReview flag, otherwise NextButton, after IsNextEnabled
   publication. Do not recompute the step flags after view, command, property or text callbacks.

Preserve original partial effects, exception identity and nested calls. Equal bound outputs still
evaluate their right-hand sides; the view, command notification and localization still occur on
repeated updates. The mix of earlier local observations and later live reads is intentional preservation.

## Verification and limits

Write tests first and observe missing-component RED. Linked-source tests use a traced mutable
source, the existing view contract and a command-notification action; no WinUI or settings class
is needed. Cover all steps/unknown values, running/report combinations, lazy report/Next branches,
constructor coldness, repeated GoToStep calls, state-before-view callbacks, command notification on
every assignment, no step/cancel PropertyChanged, sender/current values, snapshots before view and
live CanCancel/Next reads after it, nested calls, localization mutation, exceptions/partial state/retry.
Do not assume running directly disables IsNextEnabled: the visibility record and bound value have
different original rules. A controlled early CanCancel cache before the view callback must fail.

Restore exact bytes/hash and pass focused/full suites, normal Release/x64 GUI, source/compiled API,
exact footprint/policy/adapter/constructor/host-read guards, governance and redacted scans. Link the
new owner/interface and existing view/enum/record files into tests; keep the real adapter for native
composition. Preserve normal output baselines before rebuilding.

Extend accepted 166 into a NEW immutable native fixture with all old labels/case bytes and the same
21 cut paths. The one fixture-specific expected EXE literal changes; other cut bodies remain intact.
The new owner/source/MainPage/view/RelayCommand stay real and uncut, with a controlled two-method
view and inherited inert operating ports. Check actual command identity/sender/CanExecute state and
repeated notifications without Execute, private entrypoints through delegates, current step changes,
view snapshots/later Session reads, nested callbacks and partial exceptions. Add no operating IO,
cut, lifecycle call or user setting write. Derive counts/pins, independently review before build and
launch, inspect actual PE/non-admin status/resources/full hashes, acquire a NEW explicit UI lease,
then verify exact runtime corpus/COMPLETE/exit/streams/IO/hash/absence/process cleanup and release.

Fresh final review, exact final-head stacked draft checks, actual PR Project readback and immediate
completed/Done acceptance finish only this local contract. Full Page/XAML/focus/activation, installed
product, real operating routes, GC collection, G1, broad integration and release remain separate.
Retain all refs/worktrees/archives/accepted and rejected evidence; no destructive cleanup is authorized.
