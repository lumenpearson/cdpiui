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

## Native and delivery status

Native qualification and final delivery are pending at this product checkpoint. The next step is a
fresh source-pinned fixture that retains all154 accepted groups and their case bytes, with the same21
inert boundary cuts. New actual-host cases must verify the facade and synchronous bridge, including
equal values, sender identity, callback mutation, nested disclosure and exception partial effects.
Independent review and a new explicit UI lease are required before launch. No actual native result,
full Page visual result, XAML Loading, installed-product acceptance, G1 acceptance, merge or release
is claimed here.

The actual Project7 intake was read after reload under exclusive grant107: In progress, P2,
Architecture, Medium, G2, lumenpearson and exactly two canonical labels. The queue released108.
The [intake receipt](https://github.com/lumenpearson/cdpiui/issues/219#issuecomment-6794116445)
records that private Project result. CI metadata does not verify private Project fields.

All accepted and rejected artifacts remain in the ignored workspace for this plan. No prior fixture,
worktree, branch or evidence directory is deleted. Local acceptance requires the later final review,
stacked draft checks, six-section acceptance record, completed issue and actual ProjectDone readback.
