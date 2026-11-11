# MainPage progress presentation verification

## Scope and source

[Issue219](https://github.com/lumenpearson/cdpiui/issues/219) accepts a local extraction of six
MainPage display values. The baseline is frozen PR218 at
`3da11cb098d3e1241b2a86cd86ee0c14d80b1942`. The isolated branch is
`dev/blockcheck-progress-presentation`, owned by Codex chat
`01a1bc96-ea11-73a1-a4a1-5cd6db1617fa`. Parent issues13/14/15 remain open;
draft PR216 and218 retain their separate integration boundaries.

The new internal BlockCheck2ProgressPresentation owns four formatted strings, disclosure visibility
and the ViewMore/ViewLess text. It stores eight lazy ports. The six public host getters and private
setters forward to that owner. Five existing method entrypoints also forward. One synchronous
PropertyChanged bridge keeps the host sender and current value visible to observers.

Only MainPageViewModel.cs and its Progress partial change in existing product source. The owner is
one new file. Core, AddOns, Shared, numeric progress167, duration formatting, lifecycle, navigation,
completion217, resources, package versions and loaded/unloaded subscriptions are unchanged.
Product checkpoint: `41585746c9171aac7827760332c8f79f919f6f48`. The four MainPage partials
decreased from785 to740 lines; the Progress partial decreased from123 to71 lines.

## Managed behavior checks

Windows and .NET SDK8.0.427 were used. New tests were written before the owner existed. The first
focused run failed with the expected CS0246 missing BlockCheck2ProgressPresentation. After the
implementation, all42 focused cases passed. The tests cover the six cold values, all phase branches,
null and unknown phases, read/publication order, ambient formatting culture, localization mutation,
terminal short circuits, repeated equal output, all setters, instance isolation and partial failures.

A controlled mutant read current and total before phase publication. It failed11 cases and passed31.
The failures included stale counter values after phase notifications and counter localization.
The exact original bytes were restored, with SHA256
`2797C9B7346C6BDA436CAC6CF0A042EAE5837D5E3A065CC48C410D30E179C7E8`.
The restored focused run passed42/42 in20ms. The full suite then passed2243/2243 in9s,
with zero failures and skips.

```text
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BlockCheck2ProgressPresentationTests
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore
C:/Users/lumen/.dotnet/dotnet.exe build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore
```

The normal WinUI build passed with zero warnings/errors in42.52s. Its previous accepted DLL was
copied before this build. Historical217 runtime checks retain their historical normal-output hash;
they must not be executed against a newly rebuilt output and presented as current evidence.

## Contract and footprint checks

The source inventory compared613 baseline GUI C# files with614 current files. All451 original
exported source type signatures remain. The compiled comparison preserved452 GUI and88 Core
exported type shapes, with zero removed/changed shapes and zero additions on retained types.
These checks do not certify every annotation, attribute, native ABI or runtime behavior.

A source guard compares literal policy tokens under the eight declared lazy-port substitutions.
It also checks all six cold fields, host private setters, five forwards, the exact constructor
wiring, one synchronous bridge, unchanged completion body and every previous constructor statement.
The first guard attempt incorrectly replaced the Text suffix inside method names. A word-boundary
replacement corrected the verification script; product source did not change for this tool failure.
The corrected guard passed.

## Actual native qualification

The new fixture is `F:/cdpiui-verify/cdpiui-blockcheck-progress-4158574-20261111`, archived from
the full product pin above. It contains1584 Git inputs, exactly21 retained boundary cuts and37
allowed extras. All31 prior case partials retain their bytes. There are33 bootstrap files: the main
bootstrap,31 prior partials and the new ProgressPresentation partial. The owner and MainPage remain
uncut. The six new groups use actual Session/progress fields, private delegates, duration formatting,
localization and the synchronous bridge on the real dispatcher. The two-method fake view proves
composition. It does not load the actual Page or establish visual/XAML behavior.

An independent read-only review found zero Critical/Important isolation defects before build. The
native build then passed0 warnings/0 errors in60.27s. A second independent check read the actual
AMD64 PE32+ manifest: asInvoker and uiAccess=false. The current token was non-admin and the exact
executable was absent. The reviewer reconstructed all1584 inputs/cuts/extras and rehashed all522
binary-output files and all60 approved inputs. Root independently rehashed these before the lease.

The60 pins comprise5 owned binaries,20 resources and35 source/evidence files. All20 resources match
generated bytes and5206 source-JSON entries. All24 relevant English keys are nonempty. Selected pins:

| Artifact | SHA256 |
| --- | --- |
| Native GUI DLL | `6D9C0EA87245C1FE3DBB1643705418742E57E1C8ADF41F858164981B259EAF19` |
| Runner | `58F00954477C97EEBAAF1B7E2937ED2328E24F913D969BEC64F1AD4218BB8EBC` |
| Runtime verifier | `27E154518F7BCAF014262D07BB504D5946B1C2FAE05CCF7B089A7181D12268C9` |
| Complete output baseline | `C9CD5B1882AE57AF8A2EEB2AE31C2C74353815D017F6E808D87FDE20F6C46939` |
| Accepted corpus | `B903AFB4F22ED8FAA2B5D59EA18568CCEDADE648986F0A2C41547DD5E2775DE0` |
| New case partial | `BFFA558CDD39C8FD9FA28097C735075127EDD2AA1786908F871EE8F0FDD57F6C` |

Fresh queue112 was released with all six participants paused. A guarded transfer granted Root113
only for this process. One actual process, PID88728, passed160 unique groups with the exact154
prefix, one COMPLETE, zero FAIL lines, exit0 and empty stdout/stderr. The retained Root137 prefix
and Peer141 projection also match. All source/resource/artifact pins and all522 prior filenames and
hashes passed after exit. Exactly six new names increased the output count to528. The three literal
inputs retain lengths16/16/15. The two real serializer reports retain exact354/260-byte golden data,
hashes and current model fields. Settings, custom-site and forbidden export markers remain absent.
The normal GUI DLL remains `1E4FEF4A5FB5FE62543F345FA9D8DB38ABFEF61867F46BF85FF0BE38A55B2205`.

Root separately verified PID88728 and the exact executable absent, wrote idle113/requestfalse and
released the queue to114 with owner:null and all six participants paused. There was no desktop input,
elevation, OnLoaded, scan, backend/history/cleanup operation, settings write or deletion. Existing
inert settings constructor reads and normal collaborator construction remain part of the fixture.

The six new groups directly cover cold values, all private setters, host sender, equality, phase and
duration entrypoints, ordered callback mutation, independent instances, nested disclosure and observer
exception identity/partial effects/retry. A distinct positive overall estimate and a settled ordinary
ViewLess toggle have source and managed-test evidence only. This Minor native coverage limit is
retained explicitly; it is not attributed to the native assertions.

## Delivery status and limits

Final review, stacked draft final-head checks and local acceptance are pending at this evidence
checkpoint. No full Page visual, XAML Loading, installed-product, G1, merge or release acceptance is
claimed. Raw recipes, pins, logs, manifests, baseline and postchecks remain in this plan's ignored
workspace `.superpowers/sdd/2026-11-11-blockcheck-progress-presentation/`.

The actual Project7 intake was read after reload under exclusive grant107: In progress, P2,
Architecture, Medium, G2, lumenpearson and exactly two canonical labels. The queue released108.
The [intake receipt](https://github.com/lumenpearson/cdpiui/issues/219#issuecomment-6794116445)
records that private Project result. CI metadata does not verify private Project fields.

All accepted and rejected artifacts remain in the ignored workspace for this plan. No prior fixture,
worktree, branch or evidence directory is deleted. Local acceptance requires the later final review,
stacked draft checks, six-section acceptance record, completed issue and actual ProjectDone readback.
