> Historical local-composition record. The maintainer's later integration request supersedes this stage's local/draft-only delivery boundary.
> The [current integration plan](../plans/2026-11-12-class-stack-through227-integration.md) preserves the original issue14 criteria and separate G1/G3 acceptance. Original pins, results and limits below remain historical.

# Accepted class decomposition integration

Owner: Root under [issue14](https://github.com/lumenpearson/cdpiui/issues/14). Existing Project 7
intake, verified under UI grant v87, confirms In progress/P2/Architecture/Medium/G2/lumenpearson, type:refactor and
area:architecture before implementation. The original issue criteria remain open.

## Sources and delivery boundary

Preserve frozen `dev/audit-class-decomposition` at `eebc7e8621b85fa08ee015ea3b7eab1e72c78c5f`,
product `ed390cd416b58ea404f04286f7eace5a66e4320a`, and the peer branch/worktree at final docs
`249bba0143f5929ce5cb75fc09a4ef778aede731`, product `f706f16934695bcb122de63d0442c4ab20f42869`.
Root local209/211/215 and peer208/212/214 are accepted separately. Do not treat rejected9e63b55
as an accepted dispatcher implementation; consume the complete corrected final peer blobs.

The GitHub API and local origin/main agree on current default
`6c00c789ee2b37238af30f14cfdaf0f55a278274`. Start new `dev/audit-class-integration` from this
default in the existing isolated Root worktree. Keep all earlier refs and archives. No shared-main
rewrite, deletion, forced push or automatic merge to main is part of this plan. Publication of a
reviewable PR and exact-head CI are within issue14; acceptance remains open if any original criterion
is unverified. Full installed/visual and broader native acceptance remain under issue15.

SecurityEE `ee4771675956fe213634675f37579362efb7e0eb` is not an ancestor of current main. Its
2320/2398 source compositions are historical compatibility evidence. Do not transplant that entire
branch over current upstream security/localization code. A read-only whole-branch merge-tree probe
found179 conflicts, mostly unrelated security, process and documentation paths.

The new composition preserves the CURRENT default API, not the unreconciled G1 branch API.
Compared with the separate Root/G1 snapshot, current main already has one GUI optional-argument
change (localized emergency-window defaults) and three Core MSI/pipe signatures without the
expectedSha256 parameter. Source comparison proves exactly the same differences before and after
this composition; none is introduced by the decomposition. Do not claim zero drift against the full
historical Root/G1 surface. Issue13/PR10 remains open and draft for G1/ancestry reconciliation; the
historical audit-defects checkout/EE ref remain intact, with no confirmed active G1 execution chat.
Do not take over those security files. Publish this G2 change as a draft with the dependency visible;
do not mark14 accepted or merge to main while its required reconciliation/acceptance remains open.

## Scoped source composition

Derive Root's exact changed paths from base `f39e8a0a7267e4fcd4a02eef186a3ee08df8dcd9` to
producted390, restricted to CDPIUI/CDPIUI.Core/CDPIUI.Tests. The184 paths overlap current default
changes at only App.xaml.cs, the test project, and its two README files. Preserve current default
outside the declared decomposition delta, including all security, localization and workflow files.

For uncontested paths, copy the exact reviewed Root blob. For App.xaml.cs use a recorded three-way
merge (default / f39 / Root); the read-only probe is clean and retains both default emergency/theme
recovery and StartupInitialization plus all Root owner construction. Verify semantic method bodies,
not only required substrings. Keep default README prose and append current test guidance if needed.

Start test XML from current default, which intentionally removes the old DirectoryAclGuard source
link and adds EmergencyRecovery/StartupInitialization. Append only Root's additive item delta from
f39, then the peer additions. Deduplicate by item kind+Include; reject conflicting metadata,
package-version or property edits. The accepted additive delta includes the existing Unidecode.NET
test reference at the unchanged central2.1.0 version, needed by the linked registry; restore assets
after composition. Keep every default item, deliberate removal and nonadditive property unchanged.
Never restore a removed default link merely because it existed in the older Root baseline.

Then apply the peer's10-path scoped delta from common
`a5a1b9ba512073d75267943cafba9bc1eddb86b3` to finalf706. It owns App.Modal, ConfigMaker main,
CreateNewConfig.Navigation, three new System helpers and their three test classes, plus additive
project links. Each existing host must still equal its expected common/Root content before replacing
it. Preserve the sync Action and async Func<Task> dispatcher paths and rejecting-context tests.
No new product behavior or rule framework is proposed. Do not create tests that merely mirror a merge;
run the accepted behavior suites on the newly composed source.

Record every repository path's source pin, byte hash, merged XML and the precise exceptions. Import
owned decomposition documentation without overwriting newer upstream governance/localization docs.
Retain source provenance and prior contributor history through explicit source references and frozen
branches; this scoped transplant is a new integration commit, not a claim that prior branches merged.

## Verification and native union

Run fresh current-default tests/GUI first and preserve its compiled API inventories. Then qualify the
new composed SHA with focused state-owner tests, full suite, Release/x64 GUI and source/compiled
public contract comparisons against current default and the accepted Root surface. All current default
source outside declared inputs must remain exact. Investigate a failure before editing its cause.

The proposed union corpus is149: Root137 exact prefix, then relay4, named-loader4 and dispatcher4.
Project its labels onto the accepted peer141 corpus and require the same order. Keep old case bytes
where private probes still apply; any necessary private bridge adaptation must be explicit and preserve
all assertions, with exact original/delta evidence. Never infer a union pass from prior137/141.
Expected31bootstraps: Root27 case partials + peer3 + one main; verify actual filenames and duplicates.

Prepare a fresh immutable archive only after source checkpoint. Derive exact source cuts before build;
the initial proposal retains the21 existing cuts, including only the two preference endpoint files.
Current default publishes pending StartupInitialization.Ready before real startup. The fixture replaces
real startup, so complete this actual owner with Start(() => Task.CompletedTask) inside its existing
App constructor cut; do not remove or replace the production readiness helper. This is fixture setup,
not proof of real startup. Localizer setup reads only owned packaged Strings using the current builder;
do not call the real settings-reading application initializer. Verify generated JSON/resource packaging.

Keep typed in-memory Search/font endpoints, separate Search constructor and explicit font evidence,
owned logger restoration before dispatcher groups, and synchronization-context restoration in finally.
Review all constructor transitive paths and current resource inputs. No real backend/preferences,
desktop input, activation of foreign windows, privilege request or real component execution.
Measure the actual fresh binary filename count; preserve all prior filenames and allow only the same
six owned input/log/report additions. Pin sources/resources/binaries/runner/verifier before launch.
Require independent pre-build source and post-build prelaunch reviews and a new exclusive UI grant.
Run once, retain all149 results/streams/IO/hash/process proofs, then idle/requestfalse/release.

Existing unloaded selector graphs may remain event-rooted until the bounded process exits. Timer
stop/unattached state is a narrower claim than full lifecycle or GC detachment. Native corpus coverage
does not establish full startup/visual/installed behavior. Preserve all prior and rejected artifacts.

## Review and publication

Fresh independent review must inspect default readiness/emergency behavior, all accepted state-owner
contracts, exact source/XML provenance and runtime proof. Resolve actionable findings with meaningful
verification. Refresh the default branch before publication and handle any new overlap explicitly.
Prepare the PR description and required metadata, publish the isolated branch, and verify checks on
the exact final PR HEAD. Record integration only when actually performed; no broad issue14 closure
from a local commit, larger test count or successful bounded fixture alone.
