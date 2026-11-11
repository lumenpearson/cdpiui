# BlockCheck2 completion presentation verification

## Source and acceptance boundary

[Issue217](https://github.com/lumenpearson/cdpiui/issues/217) owns this LOCAL stage on
dev/blockcheck-completion-presentation, based on frozen draft PR216 at
36235846fb116acad49b328b29120afa92714712. The user authorized autonomous safe decomposition.
The [design](../specs/2026-11-11-blockcheck-completion-design.md) and
[plan](../plans/2026-11-11-blockcheck-completion.md) were introduced in
9262400ec10f6c18c6e23bcfece2586957adb759 after independent builder acceptance0Critical/Important.
The builder clarified that Session still constructs collaborators; inert ports prevent real
settings access and backend/history operations. No OnLoaded or real scan is needed for this stage.

Actual [Project7 intake](https://github.com/lumenpearson/cdpiui/issues/217#issuecomment-6792498881)
was reloaded under exclusive grant99: In progress/P2/Architecture/Medium/G2/lumenpearson,
type:refactor and area:architecture. The existing card was reused and grant100 was released.
Product checkpoint:08678c28f73dfe5c1c154ea112173ef55baa77a1. Later report changes contain evidence
only; the actual native fixture is pinned to this product source. The full local acceptance still
requires fresh whole-change review and final metadata below.

## Task1 managed policy and host extraction

The new internal BlockCheck2RunCompletionPresenter contains only the final notice policy.
HandleRunCompleted retains its InputInvalid early return, step callbacks, notice-open publication
and final navigation update. Constructor wiring stores eight lazy ports after CancelCommand;
the host callback keeps severity/title/message setters together and in their original order.
No public signature, resource, storage/scan policy, package or dependency version changes.
The MainPage partial sum is826→785 physical lines, Progress174→123 and presenter85.

Windows/.NET SDK8.0.427 commands:

```text
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BlockCheck2RunCompletionPresenterTests
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore
dotnet build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore
```

The first focused run failedCS0246 for the intentionally absent presenter, before implementation.
After the pure policy was added,38/38 focused tests passed. The tests cover every enum state,
exact localization keys/read points, no-bypass short circuit, HTTP-only after title, separate
operation/history guard/value reads, raw whitespace/null values, exception identity and reentry.

A controlled history-cache mutant failed2 behavior assertions: localization could no longer
replace the later history value or change it to null. The original file was restored byte-for-byte.
Restored source SHA256:1B991D88EB76FE797A18D8FE9BF464B16696A738F5126A3C6037FF051E5793C9.
The restored focused run passed38/38/23ms. Full suite passed2201/2201/10s,0 failed/skipped.
The normal GUI build passed0 warnings/errors in58.84s.
Normal artifact SHA256:185FED074A7B53AFCCB66D121CFE30AF29FEEED1BF9A2B7D0B19733A7D98E593.

The source guard compares the original policy tokens under the eight declared port mappings,
including the significant single-space warning separator. All remaining Progress code and all
prior MainPage constructor/host bytes match the baseline after removing the exact new wiring.
Its first attempt incorrectly expected a closing brace outside the selected verification slice;
only the tool expectation changed, then the complete guard passed. No product repair was needed.

Source inventory:451 retained exported GUI types across612→613 tracked C# files,0 lost/changed
signatures. Compiled inventory:452 GUI and88 Core retained exported types,0 lost/changed/added
retained shapes. The exact product footprint is two changed MainPage files and one new presenter;
Core/AddOns/Shared source is unchanged. These inventories have their recorded syntax/metadata
limits and do not prove every attribute, native ABI or behavioral scenario.

The baseline GUI artifact is the previously accepted product binary with D1B46B hash; the new
build legitimately replaces the normal output. Historical fixture postchecks must not be rerun
against the new normal output as though an old artifact path were immutable forever.

Raw local evidence is retained in .superpowers/sdd/2026-11-11-blockcheck-completion: focused and
full logs, rejected mutant output, exact restoration file/hash, normal GUI log, source-policy guard,
source input/footprint and compiled inventories. Old integration and native evidence remain intact.

## Task2 actual native composition

Fresh fixture:F:/cdpiui-verify/cdpiui-blockcheck-completion-08678c2-20261111, source08678c2.
Independent pre-build and post-build/prelaunch review each returned0Critical/Important/Minor.
The reviewer reconstructed all1579 Git inputs, exactly21 retained boundary files,32 bootstraps,
36 source extras, full manifest/patch diff and30 byte-identical prior case partials. The main
bootstrap changes only its fixture path/pin/result name, one call and COMPLETE count. No new
product cut was added; the presenter and both changed MainPage files remain real and uncut.

The154 unique expected labels retain the entire accepted149 prefix, including Root137 prefix
and ordered peer141 projection, then add five actual MainPage ViewModel groups:

- InputInvalid still returns through the input-notice path after its step callback changes state.
- Completed/history and no-bypass/HTTP notices use actual localized values and host setter order.
- Navigation and notice-open callbacks replace current state/error before the real policy reads.
- Reentry from the severity notification keeps the inner severity while the outer title/message
  continue, with host sender identity and two navigation updates. Equality suppression remains
  confirmed by the unchanged SetProperty source; the new case does not count the repeated
  IsResultNoticeOpen=true assignment separately.
- A title-notification exception retains identity and already-published fields, skips the remaining
  message/navigation work, then permits retry after that observer is detached.

These cases bind the actual private HandleRunCompleted as an Action on the Application dispatcher.
Session, result state, localizer, presenter and setters are uncut. A narrow two-method fake view
records GoToStep and ShowNavigationButtons; no actual Page visual or XAML Loading is exercised.
Existing inert settings ports cover constructor reads. Normal collaborators are still constructed,
but preparation/scan/history/backend/cleanup/settings-write counters remain unchanged. All added
property subscriptions and view callbacks detach in finally. The existing unloaded selector graph
has the prior process-lifetime limit; no new garbage-collection or full-startup claim is added.

Native build:0 warnings/errors,85.25s. The actual58 pins contain5 own binaries,20 packaged resources
and33 source/evidence files. Every522 preexisting binary filename/hash was captured before launch.
All20 packaged resources equal generated bytes, all5206 entries equal committed JSON, and all10
completion keys used by the native cases contain nonempty English text. Embedded PE manifest:
x64/asInvoker/uiAccess=false; the executing account is not elevated.

Native DLL SHA256:F890879CC2160F06C64BD42A0F1178114F6899024E8CB3BF8EC39E276DA3D960.
Runner SHA256:26925C43D24FCEBCA17793E0119FFC934FE150843E199F7C1BF7C0CDDBD19ED1.
Verifier SHA256:BB410DD93F571DD866C4775B8C846FEC4B4014FFC8B40132846FB19FED8185A5.

After READY, fresh queue100 was released/all six participants paused with no other ready request.
The canonical readiness and explicit Root grant101 were guarded, written and read back. A stale
preparation-only sentence in the scope was detected and corrected in the owned request/lease
before any launch; both original and corrected receipts remain. The actual readiness/pin/owner/hash
fields did not change during this wording repair. The reviewed runner checked the active lease
and matching ready request before evidence writes and again immediately before launch.

One hidden owned process, PID78236, ran UTC2026-11-11T10:22:47.1555762Z→10:22:52.4878259+00:00.
Actual result:154 unique PASS in the exact expected order, one COMPLETE,0FAIL, exit0 and empty
stdout/stderr. All58 pins and all522 prior hashes remained unchanged. Source/cut/case proofs and
post-run resource comparisons passed. Three literal inputs16/16/15 bytes and two golden serializer
reports354/260 bytes retain their exact bytes/hashes/fields. Exactly six new names produced522→528
files; forbidden outputs remain absent. Normal GUI185FED remains unchanged. PID78236 and the exact
fixture executable are absent. Idle/requestfalse/completed/allUIpaused preceded guarded101→102
release; owner:null/released was read back and the canonical request records the release acknowledgement.

No CUA, desktop input, foreign window, elevation, installation, real preferences or backend/history
operation occurred. The native boundary proves ViewModel composition and the retained old corpus;
it does not accept interactive dialogs, real scans, XAML Loading, installed behavior or G1.
Raw evidence includes native-build.log, native-approved-pins.json, native-build-baseline.json,
native-accepted-corpora.json, native-execution.log, blockcheck-completion-host-* files, source manifest,
resource post-proof and explicit corrected101/released102 receipts in the dedicated stage workspace.

## Final review and acceptance

Fresh independent reviewer /root/completion_integration_review accepted the whole change
3623584..11957046a38f9db56b9b37cc6d79082766c062af:0Critical,0Important,1Minor.
The reviewer independently reconstructed the source/read/order/nullable/exception contract, all
58 pins and522 prior hashes,154 labels and149/137/141 corpora,30 previous case bytes,21 cuts,
resource5206 entries, document origins and grant/idle/exit receipts. Build/tests/apps were not rerun.
The Minor identifies the uncounted repeated notice-open notification in the reentry case. This
report now attributes equality suppression to unchanged source, and the additional runtime counter
remains deferred. No product, case, fixture, binary or evidence change was needed.

Stacked draft publication, exact final-head CI, actual PR Project fields and the case-specific
acceptance/ProjectDone readback remain pending at this evidence checkpoint. Issues13/14/15 and
draft PR216 retain their separate integration, G1 and wider native boundaries; no main merge or
release decision is implied. Their tracker receipts hold subsequent publication/acceptance state.
