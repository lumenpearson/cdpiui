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

Product checkpoint: `ca1b6b0882babd907d1557c785f542dcba1d30bd`. Later evidence updates do not
change product or managed test bytes.

## Native preparation and retained failed build

The first fresh fixture is `F:/cdpiui-verify/cdpiui-store-download-ca1b6b0-20261111`.
Independent pre-build reconstruction verified 1603 tracked inputs, exactly 21 inherited cut paths,
36 bootstrap files, 40 extras and no reparse entries. All 34 accepted navigation R2 partials are
raw-byte identical. Twenty whole cut files are raw-byte identical; DirectoriesManager differs
only in the exact new EXE literal. The 172-label prefix and six new labels form 178 unique groups.

The initial review found stale navigation scope and reviewer text in the copied queue helper.
That metadata and the preparer were corrected before any request; original bytes were retained.
The review found no other unresolved issue and permitted build, without claiming compilation.
The build then failed: zero warnings, primary CS0136 for a conflicting local count declaration,
secondary MSB3073, 50.73 s. The initial source review had missed the declaration-space conflict.
No application, readiness request, lease, runtime pin set or successful runtime result existed.
The failed source/archive/case/recipes/build artifacts and log remain unchanged.

A NEW `-r2` fixture and `native-r2/` evidence workspace use the same product pin. Its case changes
only two identifier occurrences: the later count declaration and use become guardedEventCount.
Assertions, labels, endpoints and flow are unchanged. A fresh independent reconstruction verified
all inputs/cuts/cases/corpus and the exact rename. Read-only Roslyn 4.8 AST inspection found 33 local
declarations, one ancestor-scope conflict in R1 and zero in R2; lambda/catch/deconstruction scopes
were also checked. This was not compilation. R2 pre-build READY has no unresolved findings.
The corrected case SHA256 is
`2DD2B0ECFFB99CD93D30798CF5A9739D5A01CB0CFB94C4EA70403CA339779FCF`.

## Accepted R2 native qualification

The fresh R2 fixture is `F:/cdpiui-verify/cdpiui-store-download-ca1b6b0-20261111-r2`.
Its build passed with zero warnings/errors in 59.33 s. Pinning produced 73 actual inputs:
5 own binaries, 20 resources and 48 source/evidence files, including unchanged Queue.cs.
The full 522-file output baseline is hashed. All 20 packaged resources equal generated bytes,
5206 entries equal source JSON, and 51 English keys satisfy the declared rules. Ordinary labels
can legitimately equal their keys; all eleven actual Store values have explicit native assertions.

The same independent reviewer rehashed all 73 pins and 522 baseline paths/digests, reconstructed
all source/corpus evidence and parsed the actual EXE: AMD64 PE32+, Windows GUI, one embedded
asInvoker/uiAccess=false manifest. The current token was non-admin; the exact process, future
outputs and markers were absent. PowerShell parsing had zero errors and no pending pin remained.
Prelaunch verdict: READY, zero unresolved Critical, Important or Minor findings.

| Artifact | SHA256 |
| --- | --- |
| Native GUI DLL | `8DBC45E583E65C29E964C09F244C78855032BE5F04EA91A8B890FAAA1B5095B3` |
| Runner | `A8C9B035C59458010F049896147D8843DC11C6AA540922D6F93D5176450F35BD` |
| Runtime verifier | `7F7DCE1162D2B5896C65B88039B40B1F7180841587CB85EC6DD4A89F15B8C3C4` |
| Full output baseline | `F587E846019E311B8FDCACE32CBF36CC54FF216D9082D1954D5E4519EAE760A7` |
| Corpus | `C6361DC856CB14A5EB852C576F849CF55AC4CBC7B3E693BD29052FAEF5A7E479` |
| Corrected Store case | `2DD2B0ECFFB99CD93D30798CF5A9739D5A01CB0CFB94C4EA70403CA339779FCF` |

Root independently repeated the immediate hash/readiness checks before explicit grant 139.
One PID 79748 ran from 2026-11-11T15:41:49.4721170Z to 2026-11-11T15:41:52.0697273+00:00.
All 178 unique groups passed with the exact 172/166/160/154/149/137 prefixes and peer 141
projection, one COMPLETE, zero FAIL, exit0 and empty stdout/stderr. All 73 pins, 522 prior
filenames/hashes, full source boundaries and resources passed afterward. Exactly six new names
made 528 total files. Three literal inputs retain 16/16/15 bytes; both serializer reports retain
exact 354/260-byte golden contents and current model fields. All seven forbidden paths and
custom markers remain absent. The normal GUI retains the recorded 78D188F6 hash.

The six Store groups exercise actual constructor/dispatcher/packaged URI, eleven private setters,
host sender/equality/plain latch, stage/carryover and five-publication order, guarded entry with
later outer effects, current nested latch before outer ErrorName and after outer failure, all four
error-observer boundaries/partial state/retry, raw NaN and null speed, ordinary time thresholds,
unconditional repeated tip and independent models. Actual Store host/owner/localizer are uncut.
The view throws on all four endpoints. IsLoaded, StoreId, AdditionalInfo, handler connection/version
and inherited operating counters remain cold. All external handlers detach in finally.

Root separately checked PID 79748 and the exact EXE absent, wrote idle 139/requestfalse with
no pending call, and read back guarded release 140 with previousVersion 139, owner null and
all six participants paused. R2 evidence is retained in `native-r2/`; R1 remains the failed build.
Bitmap evidence is URI-only. No Load, Execute, queue delivery, StoreHelper, DB, installation,
deletion, new operating cut or operating IO was introduced. These checks do not establish
image decoding, full Page/XAML, real queue delivery or unchanged operating actions.

## Independent final review

Fresh reviewer store_download_final_review_available examined the complete range
`12ef689e0968bd7ff4b27cee0fe7af02091286da` through
`604c6a763fe8db8fa25a6436c738e5d2f76ed156`: zero Critical, zero Important and zero Minor findings.
The earlier Astra review attempt stopped on a usage limit without a verdict; it supplies no
acceptance evidence. The replacement used the available explicit gpt-6.1-sol model with a fresh
context. It independently reconstructed the full host, seven policies, eleven setters and native
inputs/corpus, rehashed all 73 pins and 522 prior outputs, and checked resources, IO and queue records.
It inspected the owner logs without rerunning tests, builds, product processes or UI.

The reviewer found no actionable defect and supports the stacked draft and qualified local result.
Every broader qualification it set aside is recorded with an executor ruling and cost in the ledger.
No product, managed test, fixture case, pin or runtime output changed after that review.

## Pending delivery gates

Stacked draft/exact final-head checks, actual PR Project fields and immediate six-criterion local
acceptance remain pending at this evidence checkpoint. The issue's later acceptance record is
authoritative for delivery.
Refs #14 and Refs #225. Parent #13/#14/#15 and draft #216/#218/#220/#222/#224 remain open/unmerged.
Full Page/XAML, image visuals, queue delivery, real operating routes, runtime GC collection,
installed product, G1, integration and release remain separate. Retain every ref, workspace,
archive and accepted or rejected artifact, including both preceding navigation attempts.
Raw evidence is in `.superpowers/sdd/2026-11-11-store-download-presentation/`.
