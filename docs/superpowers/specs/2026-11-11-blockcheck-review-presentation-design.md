# MainPage review summary presentation

## Intent and accepted boundary

[Issue #221](https://github.com/lumenpearson/cdpiui/issues/221) owns the next LOCAL audit
decomposition. The baseline is frozen draft PR #220 at
`3c7f8961477dbff0b4b021493b629ba1a1a0fa05`. Its local child #219 is completed with all six
criteria, actual Project Done under grant117, and [final readback](https://github.com/lumenpearson/cdpiui/issues/219#issuecomment-6795215194).
This does not accept the broad integration, G1, installed-product or release boundaries.

Use `dev/blockcheck-review-presentation` in the existing isolated Root worktree. Actual issue221
Project intake119 was reloaded and read back before this design and implementation: In progress,
P2, Architecture, Medium, G2, lumenpearson and two canonical labels. The
[intake receipt](https://github.com/lumenpearson/cdpiui/issues/221#issuecomment-6795366566) records
that evidence. The queue released120 with all six participants paused.

The four MainPage partials total740 lines. Their Review partial still holds six strings and the
policy that formats the preparation summary. Give this responsibility one internal state owner.
The interface and adapter below are the executor's design choice under the user's instruction to
continue independently. A fresh builder must inspect this design before product code changes.

## Structure and dependencies

Create three focused files beside the existing MainPage owners:

| File | Responsibility |
| --- | --- |
| IBlockCheck2ReviewSource.cs | Read-only contract for the15 current Session values used by this policy. |
| BlockCheck2ReviewSource.cs | Direct adapter over one actual BlockCheck2ViewModel, with no copies or policy. |
| BlockCheck2ReviewPresentation.cs | Six observable strings and the literal summary update policy. |

The owner derives from the existing ObservableObject and receives:

```csharp
BlockCheck2ReviewPresentation(
    IBlockCheck2ReviewSource source,
    Func<string, string> text,
    Func<TimeSpan, string> formatDuration)
```

Store dependencies without invoking them. The source adapter stores the same Session instance
without reading a property during construction. These three constructor dependencies replace a
long list of scalar callbacks; the source remains a live view of Session, never a snapshot DTO.
This contract is internal to this display responsibility. Do not make a generic presentation layer.

The source contract has exactly these property names and types:

| Type | Properties |
| --- | --- |
| int | SelectedSiteListCount, SelectedDomainCount |
| bool | IncludeHttp, IncludeTls12, IncludeTls13, IncludeTlsAuto, IncludeQuic |
| bool | IncludeIpv4, IncludeIpv6, TestAllStrategies |
| BlockCheckRunPreset | SelectedRunPreset |
| double | RequestsPerDomain, ConnectTimeoutSeconds, RequestTimeoutSeconds, StartupGraceSeconds |

Every adapter getter must directly return the corresponding Session property. Keep repeated getter
calls repeated. Do not implement the interface on Session or change its public API, options,
selection controller or settings. Unit tests use a traced mutable source fake; native composition
uses this real adapter and Session. The existing preparation-preview record can be linked into the
test project without modifying it or its estimate model.

The new owner holds ReviewSourcesText, ReviewConnectionsText, ReviewModeText, ReviewRequestsText,
ReviewTimeoutsText and ReviewMaximumTimeText, each initially string.Empty. Writable owner
properties use the existing SetProperty. The host keeps all six public getters and private setters
as direct forwards. The private host UpdateReviewSummary(BlockCheck2PreparationPreview preview)
forwards the same reference to owner.UpdateReviewSummary(preview).

Add one readonly owner field. Construct the adapter/owner after existing progress wiring, then
attach one synchronous owner.PropertyChanged bridge to host.OnPropertyChanged(args.PropertyName).
Use no second host SetProperty, queued/batched forwarding, loaded/unloaded registration or static
event. The object graph shares the host lifetime. Preserve every prior constructor statement.

## Observable policy

Move the original policy literally, substituting Session accesses with source accesses, Text with
the text delegate and the existing FormatDuration call with its delegate. Preserve the following:

1. Publish sources, connections, mode, requests, timeouts and maximum duration in that order. Each
   later expression observes current inputs after preceding property callbacks. Equal outputs still
   evaluate their right-hand side; only notifications are suppressed.
2. Sources localizes its format before reading selected-list count, selected-domain count and the
   supplied preview.TargetCount. Do not read preview earlier or introduce a null guard. An invalid
   null preview retains the original evaluation order and exception boundary.
3. Construct the protocol array in HTTP, TLS1.2, TLS1.3, localized automatic TLS, QUIC/HTTP3 order.
   Keep every literal string, space, conditional localization and OfType<string> filter. Read IPv4
   and IPv6 after that array, then localize the connections format and join both arrays with `, `.
4. Read SelectedRunPreset for the mode name. Quick and Exhaustive retain their keys; every other
   value uses Balanced. Localize the name before reading TestAllStrategies. The second preset read
   occurs only when TestAllStrategies is false. Keep the original period/space and interpolation
   when appending the all-strategies text. Do not cache preset across these operations.
5. Requests and timeouts localize their formats before their numeric getters. Keep numeric types,
   argument evaluation order and string.Format's ambient culture after all argument reads. Do not
   preformat numbers or capture a culture earlier.
6. Use the same supplied preview for maximum duration. Read MaximumDuration for the null branch;
   the positive branch then localizes its format, reads Duration for FormatDuration, and re-reads
   MaximumDuration for EstimatedStrategyJobs and EstimatedRequests after that callback. Preserve
   these repeated reads and any resulting partial effects or exceptions.
7. Assign each field before its synchronous notification, retaining the host sender. Nested calls
   may update the same owner with another preview; the outer frame then continues with its original
   preview and current source. Propagate delegate/observer exceptions unchanged, without sticky
   recovery state, catches or a reentry guard.

## Verification

Write focused linked-source tests before implementation and observe the missing-component RED.
Cover cold dependencies, all15 source properties, literal protocol/IP combinations and order,
conditional TLS localization, preset fallback and second-read short circuit, sources/preview identity,
numeric culture, null and positive maximum estimates, per-publication changes, repeated equality,
nested updates and partial observer/port failures. Explicitly assert a positive maximum estimate
reaches the duration formatter with distinct job/request values. Test null-preview evaluation order.

A controlled early preset cache must fail an observable mutation assertion. Restore exact bytes and
hash, then pass focused/full suites and normal Release/x64 GUI. Compare source/compiled public
contracts and exact two-host-files/three-internal-files footprint. A source guard must compare policy
tokens, significant strings, all15 direct adapter mappings, six private setters, one bridge and the
unchanged host remainder. Record every failed tool attempt and correction truthfully.

Build a NEW immutable native fixture from the accepted160 recipe. Preserve all prior case bytes,
labels and21 cuts. The new owner/adapter/MainPage remain uncut. Use actual Session/options and
controlled values, real preview/localizer/duration/dispatcher, and a narrow view with the existing
inert operating ports. Exercise positive maximum formatting, live callback mutations, private
setters/sender/equality, nested previews and exceptions. Seed list-item content explicitly when
needed, so constructors do not read real files. Do not invoke OnLoaded or real scan/settings/history/
backend/cleanup operations, and add no operating cuts or IO.

Derive source/extras/corpus/pin counts from the prepared fixture. Obtain independent pre-build and
prelaunch READY, inspect actual non-admin/asInvoker/uiAccessfalse PE and all hashes, then acquire a
new explicit exclusive lease. Prove all prior output hashes, exact labels/COMPLETE/exit/streams,
resource/source/IO/absence/process checks and idle/requestfalse/release. State source-only coverage
separately from executed assertions. No full Page, XAML Loading or GC collection result is inferred.

## Delivery and exclusions

Complete fresh final whole-branch review and any required Critical/Important RED→GREEN fixes.
Publish a stacked draft on frozen PR220, verify exact final-head checks and actual PR Project fields,
then post six-section LOCAL acceptance and immediately close completed/set actual Done/read back.
Parent issues13/14/15 and drafts216/218/220 retain their separate integration and release boundaries.
No shared-main change, installation, dependency update, security takeover or destructive operation
is authorized by this stage. Retain all workspaces, refs and accepted/rejected evidence.
