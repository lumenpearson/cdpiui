# MainPage progress presentation state

## Intent and execution boundary

[Issue219](https://github.com/lumenpearson/cdpiui/issues/219) owns a LOCAL continuation of the
audit decomposition. Baseline3da11cb098d3e1241b2a86cd86ee0c14d80b1942 is frozen draft PR218;
its local issue217 is completed with all6 criteria and actual ProjectDone105/final receipt.
Use dev/blockcheck-progress-presentation in the existing isolated Root worktree. Actual issue219
Project7 intake107 confirmed In progress/P2/Architecture/Medium/G2/lumenpearson/two canonical labels
after reload, then released108. Do not change parent branches, other checkouts or G1 services.

The implementation owner selects this boundary under the user's explicit autonomous authorization.
No new user behavior is proposed. Historical issue167 owns numeric progress and the estimator;
this stage owns only the six MainPage display values that remain in its Progress partial.

## Architecture

Add internal sealed BlockCheck2ProgressPresentation:ObservableObject. It owns exactly these
existing fields/properties, with the same names, values and SetProperty semantics:

| Property | Initial value |
| --- | --- |
| ProgressPhaseText | string.Empty |
| ProgressCounterText | string.Empty |
| StageRemainingTimeText | string.Empty |
| OverallRemainingTimeText | string.Empty |
| IsAdditionalProgressInfoVisible | false |
| ProgressViewMoreText | null |

Keep all six public host getters and private setters as direct owner forwards. The owner is
internal; its writable properties do not introduce an exported API. The host constructor creates
it after the existing completion-presenter wiring and attaches exactly one model-owned synchronous
PropertyChanged bridge to host.OnPropertyChanged. No bridge is added by OnLoaded/OnUnloaded.
It lives with the host, has no static/global event root and needs no separate disposal mechanism.
External observers continue to receive the host as sender and read its current owner values.

Keep host ToggleAdditionalProgressInfo, UpdateProgressViewMoreText, UpdateProgressText,
FormatRemainingTime and FormatOverallRemainingTime entrypoints as forwards. Move their literal
policy into the owner; do not duplicate it. Keep UpdateLocalizedText, Session_PropertyChanged and
the loaded/unloaded subscription logic unchanged. Numeric Session progress and the existing
BlockCheck2DurationText implementation remain unchanged.

Use eight lazy ports, stored without invocation by the owner constructor:

```csharp
BlockCheck2ProgressPresentation(
    Func<BlockCheckScanPhase?> phase, Func<bool> running,
    Func<int> current, Func<int> total,
    Func<TimeSpan?> stageEstimate, Func<TimeSpan?> overallEstimate,
    Func<string, string> text, Func<TimeSpan?, string, string> formatRemaining)
```

The host passes its current Session properties, Text and the existing duration formatter.
No input DTO, snapshot, queued/batched event bridge, extra validation, catch, guard or new package
is needed. This is a coherent progress display owner; a generic presentation framework adds no value.

## Observable contract

- Construction preserves all cold values and invokes no phase/state/count/estimate/text/formatter
  port. Existing prior constructor statements remain in their order.
- UpdateProgressText publishes phase, counter, stage remaining and overall remaining in that order.
  Each later expression reads the current ports after any preceding property notification.
- Phase selection retains all six named enum branches and Preparing fallback for null or another
  enum value. Localization occurs before publishing the selected phase.
- Counter formatting calls Text first, then reads current and total; string.Format uses the ambient
  current culture at the original call point. Do not preformat numbers or capture culture earlier.
- Each remaining-time calculation reads running separately. When not running, read nullable phase;
  if it has a value, return the literal em dash without reading an estimate or calling the formatter.
  A null phase still formats. No read is shared between stage and overall.
- Stage formatting reads its current estimate and the same RemainingTimeCalculating key. Overall
  reads its current estimate before re-reading phase to choose Baseline's OverallTimeCalculating
  versus RemainingTimeCalculating. Keep that argument evaluation order.
- SetProperty equality suppression and field-before-notification behavior remain. Equal output
  still evaluates its right-hand-side ports; only notification is suppressed. The synchronous
  bridge retains host sender identity and preserves exception identity/partial effects.
- Toggle reads current visibility, publishes its inverse, then re-reads visibility for ViewLess or
  ViewMore localization. A callback may toggle again; the outer update must use the resulting value.
  Keep null/string behavior and no new reentry or error-recovery state.
- Every host instance owns independent display fields and bridge. Loaded/unloaded behavior and
  original private setters remain compatible; no user settings, storage or scan operation is added.

## Verification and limits

Write focused linked-source xUnit tests first, using the existing ObservableObject link. Cover cold
ports, all phase/null/unknown branches, exact reads/keys/culture, repeated equal updates, per-property
callback mutation, terminal short circuits, nested disclosure toggles, instance isolation and all
relevant delegate/observer exceptions. A controlled early count/estimate snapshot must fail an
observable test before exact source/hash restoration. Run focused/full suites and normal GUI;
compare source/compiled public contracts and the exact two-host-files/new-owner footprint.

Create a NEW source-pinned native fixture from accepted154. Preserve all old case partial bytes,
the existing21 cuts and old sequence; derive new counts, extras, resource/binary/source pins and
all preexisting filenames/hashes. Add actual MainPage owner/facade/bridge cases for current values,
ordered notifications with mutations, nested disclosure and partial exceptions on the dispatcher.
Retain inert settings/operation ports; do not call OnLoaded or real scans/backend/history/settings.
Review isolation before build and actual pins before launch, then acquire a new explicit lease.
Prove exact runtime labels/COMPLETE/exit/streams/source/resources/IO/hash/process/idle release.

Fresh final review, stacked draft exact-head checks and actual delivery fields precede local
acceptance/closure/ProjectDone. Parent13/14/15 and drafts216/218 keep their separate boundaries.
No full Page visual, XAML Loading, installed, G1, new GC or release result is inferred. Preserve all
workspaces/refs/accepted/rejected artifacts; no destructive cleanup is authorized.
