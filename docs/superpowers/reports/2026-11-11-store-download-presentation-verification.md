# Store item download presentation verification

## Scope and source checkpoint

[Issue #225](https://github.com/lumenpearson/cdpiui/issues/225) owns this LOCAL GUI presentation
extraction from frozen PR #224 at `12ef689e0968bd7ff4b27cee0fe7af02091286da`.
Root owns `dev/store-download-presentation` in the existing isolated worktree. Actual Project
intake 137 persisted In progress/P2/Architecture/Medium/G2/lumenpearson and two canonical labels
after reload. The [receipt](https://github.com/lumenpearson/cdpiui/issues/225#issuecomment-6798817588)
and guarded release 138 precede implementation. No other session owns this source family.

One internal ItemDownloadPresentation owns eleven display values and the plain error latch.
Its only dependency is a lazy Text function, stored without invocation. Eleven public host
getters/private setters and seven private methods remain forwards, with one synchronous
host-sender bridge. StatusTip assigns and notifies on every call, including equal/null values.
The other ten properties retain SetProperty equality. ErrorHappens has no property event.

The main source decreases from 562 to 464 lines. Queue.cs remains 122 lines, so the partial sum
decreases from 684 to 586. The new owner has 209 lines. Existing product changes are restricted
to the main source, with two mechanical remaining error-latch accesses at their original points.
CheckCurrentStatus's two late queue reads, Queue.cs formatting/subscriptions/dispatcher/version
checks, completion-route current reads and operating actions remain in the host.

## Design clarification and TDD evidence

Design commit: `a1a79fba42b7db3622ec43da315744465b67496c`. Before product code, the fresh
builder found one important overstatement: each error invocation writes its own latch last,
but a nested error can set the current latch before later outer notifications. The specification,
plan and issue were corrected before TDD. The builder confirmed that clarification resolved and
found nothing else important. Tests explicitly preserve the nested latch and both frames' effects
when a later outer observer throws. No entry snapshot or outer recheck is introduced.

The first cold test was written before the owner or link existed and observed the expected
CS0246 for ItemDownloadPresentation. All 83 focused cases were then written before implementation.
The first pure run passed 83/83 in 39 ms, with two test-only CS8620 nullable-tuple warnings.
Explicit nullable expected-tuple types corrected those annotations without changing assertions.
A clean pure run passed 83/83 in 31 ms before host wiring.

Coverage includes twelve cold values, eleven setters, raw null strings, unconditional tip events,
plain latch, ordered partial states, 22 literal stage/carryover rows, repeated localization,
single entry guards, callbacks changing the latch, NaN/infinities/raw progress, minute thresholds,
culture after localization, nested errors/stages, failure identity, partial effects, retry and
independent owners. The nested-error cases distinguish current shared state from an entry snapshot.

The first controlled-mutant preparation stopped before any mutation or test because its LF
anchor did not match raw CRLF bytes. The finally block restored and verified the original hash;
the original helper was preserved. Only the harness newline selection was corrected.
The actual early-latch mutant then failed eight cases and passed 75, total 83 in 40 ms.
Those are behavioral assertion failures, with no compilation error. Exact original owner bytes
were restored in finally and verified with SHA256
`5FD7E26EF1BE4D5C3D8851761565B9F1B11971D9281FC53925951B5CCB6E053C`.
The restored focused run passed 83/83 in 28 ms.

With host wiring present, the complete suite passed 2588/2588 in 9 s, zero failures/skips.
The normal WinUI build passed with zero warnings/errors in 47.92 s.

```text
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ItemDownloadPresentationTests
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore
C:/Users/lumen/.dotnet/dotnet.exe build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore
```

## Compatibility and exact-source guards

The source comparison covers 451 baseline exported types across 620 baseline and 621 current
GUI files. No original signature was removed or changed. Compiled comparisons preserve 452 GUI
and 88 Core exported type shapes, with zero removals/changes and no additions on retained types.
These bounded inventories do not certify every attribute, annotation, inferred source type or
native ABI. Core/AddOns/Shared source remains unchanged.

The policy guard compares all seven literal bodies under declared visibility, nullable annotation
and Text substitutions. It reconstructs the complete original host under only the allowed field,
property, method, constructor and two latch substitutions. It checks the entire owner's declared
shape, twelve defaults, eleven setter rules, plain current latch, cold constructor, one bridge,
all prior constructor statement order, unchanged Queue.cs and one additive test link. It passes
without a product correction. The old accepted native cases contain no moved Store-field reference.

The current normal GUI SHA256 is
`78D188F6BE63B731F92EBC8006160894A632389FABDE97140F5F77D77B6CD61C`. Accepted navigation evidence keeps its historical F1DC9407 output hash,
which was saved before this rebuild. Do not rerun its frozen verifier against the new normal output.

## Pending native and delivery gates

Fresh immutable native qualification will retain the accepted 172-label corpus, all 34 old case
partials and 21 inherited cut paths. Actual Store host constructor/private-forward/setter evidence
uses its current dispatcher and a four-method view substitute that throws on operating endpoints.
The packaged BitmapImage check is limited to its owned URI, without visual decoding/loading.
No Load, Execute, queue delivery, StoreHelper, DB, installation, deletion, new operating cut or IO
belongs to this stage. Independent pre-build/prelaunch READY and a new explicit lease are required.

Native, fresh whole-branch review, stacked draft/exact final-head checks, actual PR Project fields
and immediate six-criterion local acceptance remain pending at this evidence checkpoint.
Refs #14 and Refs #225. Parent #13/#14/#15 and draft #216/#218/#220/#222/#224 remain open/unmerged.
Full Page/XAML, image visuals, queue delivery, real operating routes, runtime GC collection,
installed product, G1, integration and release remain separate. Retain every ref, workspace,
archive and accepted or rejected artifact, including both preceding navigation attempts.
Raw evidence is in `.superpowers/sdd/2026-11-11-store-download-presentation/`.
