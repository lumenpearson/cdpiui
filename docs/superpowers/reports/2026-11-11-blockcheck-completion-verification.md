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
The product checkpoint is recorded by the issue evidence after the source commit. This report's
first section records Task1; new actual-host acceptance remains pending below.

## Task1 managed policy and host extraction

The new internal BlockCheck2RunCompletionPresenter contains only the final notice policy.
HandleRunCompleted retains its InputInvalid early return, step callbacks, notice-open publication
and final navigation update. Constructor wiring stores eight lazy ports after CancelCommand;
the host callback keeps severity/title/message setters together and in their original order.
No public signature, resource, storage/scan policy, package or dependency version changes.

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

## Task2 native composition and final review

Pending. The accepted149 fixture belongs to b41b724 and does not qualify the new MainPage wiring.
Prepare a fresh named source snapshot, preserve all prior case partials and documented cuts,
add actual-host completion cases, obtain independent isolation/READY review and a new explicit
UI grant. Pin actual outputs/resources/source/runner/verifier and preexisting files before launch.
No new native process, desktop action, backend operation, installation or successful result is
claimed at this Task1 checkpoint. Final independent review and all local issue criteria remain open.
Issues13/14/15 and draft PR216 retain their separate integration, G1 and wider native boundaries.
