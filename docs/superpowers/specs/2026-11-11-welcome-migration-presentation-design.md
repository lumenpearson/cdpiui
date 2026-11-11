# Welcome Migration Presentation Design

## Intent, ownership and admission

Issue #231 continues authorized decomposition from frozen draft PR230 at
`b0f2c93d66000eb02cd963cf4421173980686e95`. Actual Project intake163 confirmed In progress/P2/Architecture/Low/G2/lumenpearson/exactly
area:architecture+type:refactor after fresh navigation, fullAX/screenshot/JSON saved. Own tabs closed,
idle163/requestfalse/pendingfalse/guardedrelease164/all six UI paused verified. Fresh builder
welcome_migration_design_check returned nothing important left before product implementation. This is a LOCAL stage under14;
13/15, G1 reconciliation, integrated wizard, installed product and release remain separate.
Root chat01a1bc96-ea11-73a1-a4a1-5cd6db1617fa owns the existing isolated audit-class-decomposition
checkout. Branch: dev/welcome-migration-presentation. No other worktree is taken over.

WelcomeViewModel has983 physical lines across four partials. Migration.cs has407. Nine display
values and two synchronous policies form a coherent responsibility beside session/queue/import.
This extraction preserves behavior, rather than changing files to meet a line-count threshold.

## The owner

Add internal sealed WelcomeMigrationPresentation<TSeverity> : ObservableObject in
CDPIUI/ViewModels/Welcome/WelcomeMigrationPresentation.cs, below200 physical lines. Use the existing
MigrationComponentDisplayItem directly, without another generic item parameter or new model.
The exact constructor is:

```csharp
WelcomeMigrationPresentation(TSeverity initialSeverity, TSeverity errorSeverity,
    Func<string, string?> text)
```

Store the error severity and resource callback; assign initial severity to its backing field.
Do not invoke/validate callbacks, localize, read session/settings or emit events during construction.
There is one production closed InfoBarSeverity instance per host; no enum constraint or mapping.
Use nullable annotations on the internal owner to describe baseline raw-null behavior. Existing
public host nullable annotations/accessors remain unchanged.

The owner exposes public getters/setters with these exact names/types/defaults:

| Property | Type | Default |
|---|---|---|
| IsMigrationMode | bool | false |
| MigrationStatusTitle | string? | string.Empty |
| MigrationStatusMessage | string? | string.Empty |
| MigrationStatusSeverity | TSeverity | explicit initialSeverity |
| MigrationProgress | double | 0 |
| MigrationComponentItems | IReadOnlyList<MigrationComponentDisplayItem>? | null |
| IsMigrationComponentsListVisible | bool | true |
| MigrationSummaryText | string? | string.Empty |
| IsRetryMigrationVisible | bool | false |

All nine setters use existing SetProperty/EqualityComparer<T>.Default. Mode additionally emits
IsReadyBundlesContentVisible only after a successful changed SetProperty returns; its read-only
derived property is !current IsMigrationMode. The other setters have no side effects. Keep raw
null strings/list, unknown severity, signed zero/NaN/Infinity and custom list equality semantics.
Do not copy, clamp, normalize, enumerate or subscribe to a list. No cached derived value exists.
Raw-value preservation includes the original equality suppression: assigning -0 to initial +0
does not replace the stored bits, and a second equal NaN can retain the first NaN payload. An old
custom list whose Equals(newList) returns true remains the stored reference. Test those retained
values, rather than requiring replacement for assignments that baseline SetProperty suppresses.

## Exact display policies

Public owner ShowStatus(string? titleKey, string? messageKey, TSeverity severity) executes:
MigrationStatusTitle = text($"/WelcomeWizard/{titleKey}"); then
MigrationStatusMessage = text($"/WelcomeWizard/{messageKey}"); then severity assignment.
Keep each lookup interleaved with its own assignment and observers. A title observer can change
the message lookup result, invoke a nested status/error call or throw. Do not prefetch both texts.

Public owner ShowError(string? message, string? errorCode) first obtains a local string from
text($"/WelcomeWizard/MigrationError_{errorCode}"). If it is not null/empty/whitespace, replace only
the local message. Then assign failed-title lookup text("/WelcomeWizard/MigrationFailedTitle"),
the raw interpolated message plus literal "\n\n" and code, errorSeverity and retry=true, in that order.
No CodedMessageLocalizer, resource-key fallback, catch, formatting normalization or validation.

Original comparisons happen against current state after nested effects, and outer arguments/local
message remain the outer call's values. Exceptions propagate with original identity from the owner;
earlier field changes and nested effects remain and later steps stop. Retrying uses that partial state.
Mode observer failure prevents its derived notification; nested reverse mode still allows an outer
derived event after the first SetProperty returns. Do not guard against or suppress reentry.

## Host composition and exclusions

Replace the nine old private fields in Migration.cs with one readonly owner field. Preserve all
nine public getters/private setters as exact owner forwards, plus the derived visibility getter.
IsMigrationReadyToContinue and _migrationReadyToContinue remain host-owned and unrelated to mode.
Replace private ShowMigrationStatus with an owner forward. Private ShowMigrationError calls
owner.ShowError(message,errorCode), then the original literal IsNextEnabled=false statement.
This final eligibility assignment is host-owned, follows a successful owner return and still runs
after a nested display call. If the owner throws, the tail is not reached. Its own observer fault
retains the new Next value and all preceding display effects. Do not add a disable-next port.

In main WelcomeViewModel.cs add Microsoft.UI.Xaml.Controls using and cold composition after the
existing _view/_dispatcherQueue assignments, before command construction. Pass explicit
InfoBarSeverity.Informational, InfoBarSeverity.Error and key=>_localizer.GetLocalizedString(key).
Subscribe exactly one synchronous owner PropertyChanged bridge to host OnPropertyChanged(name),
preserving VM sender and exception propagation. All previous constructor statements retain order.

These are the only two existing product files allowed to change. Whole-host reconstruction must
prove every undeclared byte of normalized source unchanged. Preserve Localized and every session/
CTS/subscription/queue/import/readiness/retry/projection/aggregate/window/license/settings/DB/IO
method exactly, including the host's existing summary-after-list late localizer and Math.Max reads.
MigrationImport.cs and MigrationQueue.cs remain byte-identical. No WelcomeWindow/XAML/resources,
Core/AddOns/Shared/package/security/G1 implementation or exported type/API changes. No new partial.
The baseline four partials/window sources match integrated main6c00c789; unpublished G1 changes
are neither adopted nor modified. A read-only utility peer confirmed no overlap with its frozen refs.
Live main subsequently advanced to21555a29ce9fba98c678c445d04ee9f6ce3c85ba with the separately
owned PR229 language-selector fix (six paths, no Welcome). This stage keeps b0/6c00 as named frozen
inputs. It does not claim equivalence to the new main composition or borrow229 installed evidence.

## Managed verification

Link the owner, actual ObservableObject and actual MigrationComponentDisplayItem into existing
CDPIUI.Tests with an owned non-default test severity enum. Write cases before implementation and
observe CS0246 missing owner. Cover nine defaults and changed/equal/raw values, cold null callback,
mode/derived event snapshots, nested reversal/failure, same/equal-custom/distinct/null lists, NaN/
Infinity/signed zero, and per-instance isolation. Trace raw resource keys, title-assignment-before-
message-lookup, nested lookup/property effects, outer locals and every callback/property exception
boundary with exact sentinel identity/partial state/retry. Error fallback covers null/empty/white/
nonempty text and raw null message/code; error sequence has title/message/severity/retry observers.
The host Next tail is actual-native/source proof, not an arbitrary owner port test.

Controlled mutant: eagerly resolve the message before assigning title, preserving all types and
compilation. Require meaningful ordering/reentry/fault test failures, save actual exit/logs, restore
owner bytes/hash in finally and run focused GREEN, full dotnet test and normal Release/x64 GUI
sequentially. Compare451 source exported types and compiled452GUI/88Core bounded shapes against
the same normal GUI composition baseline; attributes/native ABI remain outside those inventories.
Check literal old ShowStatus/Error policy under only documented resource/severity substitutions,
exact host reconstruction/accessors/bridge/constructor/defaults/equality/footprint/untouched hashes,
governance, whitespace and redacted scans. Keep every failed harness attempt and its resolution.

## Native and delivery verification

After product checkpoint create a NEW immutable source-pinned fixture/archive from accepted
ConfigMaker184. Keep all old184 labels, raw case bytes,21 operating cuts (20raw plus EXE literal),
old outputs/resources and exact inherited corpus prefixes. Add six unique actual-composition groups:
1 cold constructor/closed typed owner/nine defaults/accessors/VM sender/current dispatcher;
2 raw setters/equality/mode-derived order/nested reversal/list identity/NaN/independent hosts;
3 actual resource title/message/severity and status reentry/current equality;
4 actual error-code localization, fallback/double-newline, retry and host Next tail ordering;
5 status title/message/severity observer faults/partial retry with private reflection wrappers;
6 error title/message/severity/retry/Next faults and nested error/status outer continuation.
Target190 =184+6; derive exact actual counts and labels before any build or run.

Construct actual WelcomeViewModel with current DispatcherQueue and a rejecting IWelcomeView; each
CurrentStep/RefreshNavigation/ShowStoreLoadingButtons/AcceptLicensesAsync endpoint throws. Never
execute commands, license flow, SetMigrationSession, ApplyMigrationSessionState, queue/import or
window lifecycle. Every instance detaches external test observers and disposes only its own actual
CTS framework resource after assertions, without running migration or claiming teardown coverage.
Private setter/helper reflection can wrap observer exceptions; explicitly inspect/unwrap wrappers
and distinguish from direct owner exception identity proved in portable tests. Actual arbitrary
localizer-null/subscriber changes are managed/source-only; native uses real uncut localizer/resources.

Require independent pre-build review of source/cases/corpus/containment, normal native build zero
warnings/errors, actual binaries/resources/source/runner/verifier pins, prior output names/hashes,
resource JSON/generated parity and independent prelaunch READY. Obtain a NEW explicit exclusive
queue grant, recheck pins/absence/actual process/nonadmin context, and launch one owned process.
Verify190uniquePASS/prefixes/COMPLETE1/FAIL0/exit0/streams0, all input/prior output/source/resource
hashes and exact new names/forbidden absence/normal GUI unchanged, then process absence/idle/
requestfalse/pendingfalse/guarded release. Do not rerun accepted fixtures or add operating cuts.

Obtain fresh whole-range independent review before delivery, record every ruling/deferred finding
with costs, publish a draft stacked on frozen PR230 and pass exact final-head checks. Read back
actual PR Project fields separately. Match six issue criteria, post six-section LOCAL acceptance,
immediately completed/actualDone/readback, then post-edit metadata status. Keep13/14/15 and drafts
open/unmerged. Retain all evidence/refs/worktrees; no deletion/reset/force/main merge/release.

## Explicit limits and builder choices

No full loaded wizard/rendering/EN-RU/accessibility or actual session/queue/import/license/storage,
cancellation/teardown/GC/concurrency, full metadata/native ABI, installed/G1/integration/release claim.
Portable callback fault permutations are distinct from the uncut native localizer. Builder may
choose private helper/test organization and assertion grouping within the named interface and six
native groups; it may not alter operating boundaries, notification order, counts or accepted corpus.
All interface/scope choices here belong to the executor under autonomous authorization, not to a
new user approval. Actual Project admission and fresh builder verdict are recorded above; product implementation has not started.
