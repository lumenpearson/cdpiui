# Diagnostic highlighting update state

Local issue215, base80c1ec2/source00f39. ActualProject69 read after reload: In progress/P2/Architecture/
Medium/G2/lumenpearson, type:refactor+area:architecture+area:native-qa. Prerequisite211completed/Done64
is verified. Root owns only ConfigMakerUserControl.Editor.cs (157lines of828 across five partials),
new Helper/UserExperience/DiagnosticHighlightingUpdater.cs, tests/source link and evidence/docs.
Peer owns ConfigMakerViewModel208, CreateNavigation212 and App.Modal/DispatcherTaskBridge214.
No XAML, other control partial, VM, Core, preferences, persistence or format changes.

The helper is a small System-only state owner, one current consumer, not a framework or a large
transplanted host. State is one last-requested signature initialized to string.Empty; Reset sets it
to string.Empty without replacing the helper. API:

`void Update<T>(string background, bool isLight, Func<IEnumerable<T>> diagnostics,
Func<T,bool> isError, Func<T,bool> isWarning, Func<T,string> token,
Action<bool,string[],string[]> apply)`.

Run diagnostics().Where(isError).Select(token).ToArray() completely before obtaining diagnostics()
again for Where(isWarning).Select(token).ToArray(). Do not fuse passes, cache the enumerable or
preproject Source: ConfigMakerDiagnosticViewModel.Source is mutable. Host predicates and selector
must read item.Source separately, exactly as before. Preserve ordering, duplicates, runtime nulls,
fault identity/timing, and current reads during collection. Reentry/reset during collection must
compare against the current signature after collection, not a signature captured on entry.

Build exactly $"{background}|{isLight}|{string.Join(',', errorTokens)}|{string.Join(',', warningTokens)}".
Compare current stored key with StringComparison.Ordinal. Unchanged keys still collect both arrays.
For a new key, store it immediately BEFORE apply(isLight,errorTokens,warningTokens). No catch/restore,
retry, queue, lock, cancellation or token normalization. A callback failure is sticky; collection
faults do not commit. Same-key callback reentry skips application; different-key inner state survives
the outer return. Preserve null/empty and comma/pipe signature collisions as existing behavior.

The host keeps its literal captured background and short-circuit forced-dark/ActualTheme/App.CurrentTheme
expression. The callback keeps CommandEditor.SyntaxHighlighting assignment, including its receiver/RHS
evaluation and highlighter constructors, BEFORE reading/assigning TextControlBoxDesigns to Design.
Do not eagerly capture CommandEditor, build a highlighter or read designs before collection. Replace
only the old signature field with a readonly helper instance and its three usages. Reset remains
after EditorPanel.Background assignment succeeds and before ApplyEditorTheme. No disposal/constructor
forwarding/new public API is needed. Per-call delegates are a bounded allocation cost, not a reason
to cache current callbacks or sources.

TDD uses plain objects/strings and traces, no fake WinUI. Cover two reads/passes, mutable Source and
collection reset/reentry, repeated-key collection, raw keys/collisions, exception identity and sticky
commit, same/different-key callback reentry, reset identity and per-instance independence. Watch
missing-helper RED and a controlled commit-after-callback mutant fail with bounded nested reentry;
restore exact bytes/SHA in finally, then focused/full GREEN. Build normal GUI and compare bounded
source/compiled contracts. Source/metadata inventories do not certify full ABI or visual behavior.

Native design fixes four new groups=137 based on accepted menu133 (a different case set from peer
named-loader137). Keep all26old case partials byte-identical and add one new partial=28bootstraps.
Use real unactivated ConfigMakerUserControl/compiledXAML/TextControlBox/VM diagnostics, assert unchanged
refresh highlighter identity, replacement after reset/token changes and independent control state.
No Loaded, navigation, focus, desktop input, renderer/frame timing or real preferences/backend.
Selector/UI callback faults and synthetic reentry remain managed/source proof, not native notification
claims. Own VM diagnostics timers/background cancellation are stopped on teardown.

IMPORTANT prelaunch isolation ruling: accepted menu133 has19cuts and does NOT cut SearchUserControl
or ConsoleFontHelper. InitializeComponent constructs SearchUserControl (two immediate SEARCH reads).
Generated x:Bind registers its ConsoleFontHelper.Instance update on Loading; constructing the
unloaded control does not prove that this singleton is initialized. The fixture explicitly accesses
the isolated font singleton separately, verifies its fixed typed values/covered read keys and two
bounded FontSize setter writes, then restores size12. Constructor Search reads use fresh per-key
baselines and must each increase once. Before native build, add
exactly two fixture-only preference boundary files: Controls/Universal/SearchUserControl.xaml.cs and
Helper/UserExperience/ConsoleFontHelper.cs. Replace ONLY their SettingsManager Get/Set endpoints with
typed owned literal/in-memory callbacks; keep UI constructors, setters, events and highlighting source
unaltered. Explicit new total21cuts, no new IO. Pin all replacements; require no remaining SettingsManager
endpoint in those patched files and reject unknown preference keys/types. Never run the real control
before this source-boundary proof. Retain the old19 boundaries/IO gates and independently review the
exact new prelaunch scope, source pins and asInvoker/freshness/process gates before any launch.
Do not modify product preferences or accepted old fixtures to obtain this isolation.

Four intended groups: real constructor/unchanged-refresh cache; real diagnostic changes and resulting
highlighter identity; real ApplyEditorBackground reset and brush/highlighter update; independent cache
state across two owned controls. If precise assertions need refinement, settle and ledger them before
building. No full visual/startup/installed acceptance is inferred from this bounded composition.

Use fresh committed-Security and available accepted peer source joint, complete Git/blob/XML proof,
full tests/GUI and independent final review. Peer214old9e is rejected after an Important sync-context
finding; only corrected/verified pins can be accepted. Keep all failures/archives/oldpins, no destructive
operations or time-based UI takeover. After all local215criteria pass, record acceptance and immediately
closecompleted+actualProjectDone/readback. Parent14/15 integration/broadernative remain open.
