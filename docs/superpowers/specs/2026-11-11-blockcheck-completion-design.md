# BlockCheck2 run completion presentation

## Intent and ownership

[Issue217](https://github.com/lumenpearson/cdpiui/issues/217) accepts a local behavior-preserving
decomposition of the completion notice in BlockCheck2MainPageViewModel. The user authorized
autonomous continuation without confirmation. The implementation owner chooses this boundary;
it is not a new behavior request from the user.

Baseline:36235846fb116acad49b328b29120afa92714712, frozen draft PR216. Use
dev/blockcheck-completion-presentation in the existing isolated Root worktree. Peer source families
are released and its frozen refs/artifacts remain intact. Actual Project7 intake under grant99
confirmed In progress/P2/Architecture/Medium/G2/lumenpearson/two canonical labels after reload.
PR216 remains draft; issues13/14/15 retain their wider acceptance.

## Chosen boundary

Extract the contiguous severity/title/message/history-warning policy after IsResultNoticeOpen=true
and before UpdateNavigationButtons in HandleRunCompleted. Keep InputInvalid handling, both step
transitions, the notice-open setter and navigation updates in that private host method.
Keep the three result setters together in a callback supplied by the host.

Use a small internal BlockCheck2RunCompletionPresenter in the existing BlockCheck2 presentation
folder. It has no WinUI, storage, settings, network, timers or mutable per-call state. Its eight
ports are current state, no-bypass, HTTP-only, operation error, history error, text lookup, issue
formatting and notice publication. The constructor stores them without reading or invoking them.
Show computes with locals and invokes the publisher once. Do not add validation, error recovery,
memoization, a DTO snapshot or a reentry guard.

This is a domain policy because the existing session states and literal resource keys define it.
A generic notification framework would add abstraction without another matching consumer.
The result-page report summary remains separate: it has different branch rules and keys.

## Observable contract

- The original host tests InputInvalid first. Its navigation callback can change current state;
  later reads still come from the actual Session. The invalid branch formats issues, updates
  navigation and returns without opening or publishing a result notice.
- The other branch navigates to WorkComplete, opens the result notice, then evaluates policy.
  PropertyChanged and view callbacks can reenter or change session data before policy reads.
- Test current state at each original if/else-if point. NoBypass is read only when that current
  state is Completed. No state read is cached across another callback or branch test.
- The no-bypass branch chooses Informational, localizes its title, then reads HTTP-only to choose
  the message key. Other branches preserve Completed, CompletedWithWarnings, Canceled and the
  default Error fallback. The chosen branch is not restarted after title localization.
- Failure localizes its title before checking operation error. Preserve separate guard/value
  reads; a value can change between them, including to null. Issue formatting is lazy and used
  only for a null/empty/whitespace guard result. It can throw with its original exception identity.
- History error is tested after the branch message. When nonblank, concatenate the current
  message, Environment.NewLine, localized warning, one space and a fresh history-error read.
  Localization can replace the later value. Keep raw whitespace and nullable value behavior.
- Publish severity, title and message in that order through the host's existing SetProperty
  setters. Sender identity stays the host. Reentry has independent locals and no guard.
  The outer call continues its original publications after an inner call returns.
- Preserve exceptions and already-observed partial effects. A failing text getter or setter
  does not get a new catch, rollback or final navigation update.

## Interfaces and files

BlockCheck2RunCompletionPresenter constructor:

```csharp
BlockCheck2RunCompletionPresenter(
    Func<BlockCheckSessionState> state, Func<bool> noBypass, Func<bool> httpOnly,
    Func<string?> operationError, Func<string?> historyError,
    Func<string, string> text, Func<string> formatIssues,
    Action<BlockCheck2NoticeSeverity, string, string?> publish)
```

Show():void is internal behavior through the existing private HandleRunCompleted entrypoint.
The MainPage constructor adds a readonly presenter after the existing CancelCommand assignment.
The host publisher performs the three original property assignments, without snapshots or extra work.
Only the new source file, MainPage constructor and Progress partial change in product code.
The test project adds one linked source item; existing severity/model references are reused.

## Verification and acceptance limits

Write focused xUnit tests before host edits. Cover every enum state, exact keys/read order,
short-circuiting, nullable values, localization mutation, nested Show calls and thrown delegates.
Observe at least one meaningful controlled read/cache mutant fail, restore exact bytes/hash and
run focused/full tests. Build the normal GUI and compare baseline source/compiled public APIs.
The current baseline2163/native149 results remain historical until the named new source runs.

Prepare a NEW immutable source-pinned native fixture from the accepted union149 recipe. Preserve
all prior case partials byte-for-byte; derive counts and pins from actual files. Add narrow actual
MainPage composition cases for invalid early return, navigation/open/publication order, callback
mutation and publication reentry/exception behavior. Existing inert settings/run ports prevent
real settings access and backend/history operations; the Session still constructs its normal
collaborator objects. Do not call OnLoaded or execute a scan. Review all cuts and the new
cases before build/launch; pin actual output/resources/runner/verifier and preexisting filenames.
Acquire a new explicit exclusive UI grant only after READY; prove pass labels, IO/hashes/resources,
streams, owned process exit and idle release. No inferred grant or broad native acceptance.

One fresh final independent review gates local acceptance. Keep rejected attempts and original
branches/fixtures. No deletion, reset, main merge, installation or G1 ownership change is required.
The local issue can close only with its six verified criteria and actual Project Done readback.
Integration/release acceptance stays with13/14/15 and later draft qualification.
