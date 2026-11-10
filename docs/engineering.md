# Engineering process

This process applies to the `lumenpearson/cdpiui` fork.
The maintainer owns triage and integration.
An assignee is the contact for the next action; assignment does not prove that implementation has started.

## Record the work

1. Search existing issues before you create an issue.
2. Describe the problem, affected revision and expected result.
3. For a defect, give inputs, reproduction steps and actual results.
4. Separate a source claim, a reproduced failure and an untested hypothesis.
5. Set a type label, an area label, an assignee and a G1–G5 milestone.
6. Add the issue to the delivery Project. Set Priority, Area, Risk and Status.
7. Record dependencies, acceptance criteria and the verification method.

Use the [security policy](../SECURITY.md) for a new unpatched vulnerability.
The public audit register contains findings whose publication the fork owner authorized.
Do not put credentials, private user data or active tokens in issues.

## Prepare the change

Use a branch named `dev/<short-purpose>` or `fix/<short-purpose>`.
Use an isolated worktree when another session owns the current checkout.
Do not stage, reset or edit another session's files.
One implementation task per active execution owner is the default work-in-progress limit.
The GitHub assignee can remain the maintainer for triage while separate sessions own isolated implementations.
Record each execution owner, branch and worktree in its issue or PR. Do not invent additional GitHub accounts.

Before you change behavior, record its contract and write a regression test that fails for the defect.
Run the test before and after the fix.
For a refactor, preserve public signatures, stored values, notification order, object identity and reentry behavior.
Record an intended behavior change as a separate requirement.

Use the commands in the [contribution guide](../.github/CONTRIBUTING.md).
CI includes secret scanning, direct/transitive NuGet audit, C# and Python CodeQL analysis, builds, tests and packaging checks.
CodeQL C# uses no-build source analysis. The separate Windows build verifies generated WinUI code and compiled bindings.
Review scan alerts before assigning a current vulnerability status; a completed scan is not a clean security review.
Run native Windows checks for changes to handles, ACLs, process launch, pipe behavior and filesystem operations.
Run native UI checks for changed XAML, dialogs, focus and lifecycle behavior.
Give each interactive test session exclusive ownership of the UI queue.

## Write commits

Use a conventional subject: `fix:`, `feat:`, `refactor:`, `test:`, `docs:`, `ci:`, `build:`, `perf:`, `chore:` or `revert:`.
Keep the subject specific. Explain the reason, resulting behavior and verification in the commit body.
Add an issue reference, for example `Refs #123`.
Record a breaking change and its migration when applicable.
Do not claim a test ran when only a previous revision was tested.

Commit objects have an author, committer, subject and body.
GitHub labels, milestones and Project fields belong to the linked issue or PR.
Do not rewrite published commit history to add tracking metadata.

## Open and review a PR

Use the [PR template](../.github/PULL_REQUEST_TEMPLATE.md).
Complete Problem, Changes, Verification, Risk and recovery, and Related work.
Link at least one existing issue in this fork.
Set exactly one `type:` label and at least one `area:` label.
Set an assignee, the owning milestone and the Project fields.

The `type:` and `area:` labels are canonical for delivery work.
Keep the ten pre-existing generic labels as historical aliases; do not add them to new delivery issues or PRs.
Use `enhancement` only for proposal intake until triage defines the work.
Map `bug` to `type:bug`, `documentation` to `type:docs` or `area:documentation`, and `accessibility` to `area:ux`.
The other default labels retain their historical meaning and associations.

New feature proposals enter through `enhancement` while triage defines their contract.
The maintainer records the canonical delivery type when the proposal becomes scheduled work.
Use a specific issue to agree any future taxonomy extension before adding another overlapping label.

Keep a draft while its acceptance criteria or evidence are incomplete.
Required checks must pass on the current PR HEAD.
Review the final diff against the issue contract.
Record independent review, findings, resolutions and the reviewed SHA.
A fresh reviewer can be an authorized independent reviewer session; do not fabricate a human approval.

The repository has one maintainer listed as CODEOWNER.
The process requires independent evidence review; GitHub approval count must not prevent that owner from maintaining the fork.
If an emergency exception is necessary, record the risk, reason, approver and follow-up issue before integration.

## Integrate and accept

Merge only after the required checks, review and scoped verification pass.
Record the resulting commit and link the acceptance evidence.
An unmerged closed PR is superseded or abandoned work, not a delivered fix.
An integrated code change can leave its acceptance issue in Verification.

Close an issue only when its stated acceptance criteria pass.
Include the tested revision, commands, environment, results and material limits in the closing record.
Reopen the issue if acceptance fails later.
Set the Project status back to the appropriate working state.

Do not infer a release decision from issue counts or a green build.
The [delivery gates](delivery-gates.md) define release acceptance.

## Recover metadata checks

The Contribution metadata workflow runs trusted code from the default branch.
It reads current PR metadata through the GitHub API.
It publishes the `Contribution metadata` status on the exact current PR HEAD.
It never checks out or executes the proposed PR code with write permissions.

PR creation, commits, body edits, labels, assignment and milestone changes cause a new check.
The checker rejects a result if the final API read detects changed required metadata or HEAD.
The API read and status publication are separate operations. The check does not provide an atomic lock on PR edits.
Use Actions → Contribution metadata → Run workflow with the PR number to recover a missed event.
After a new commit, verify a successful status on that new HEAD.

The workflow first becomes active after it enters the default branch.
Observe a successful real status before making it a required branch check.
Dependabot PRs use the same acceptance rules. The maintainer must add an issue, milestone and completed description before integration.

## Measure process health

Review the open queue each week and before candidate selection.
Check blocked age, ready work, review wait, failed checks and reopened defects.
Measure cycle time from actual Status changes; do not assign artificial historical start dates.
Review all unresolved High-risk work before a release decision.
Keep test counts attached to their exact revisions; a larger count alone does not prove better coverage.
