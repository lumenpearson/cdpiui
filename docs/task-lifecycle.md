# Detailed task intake and immediate acceptance

This policy applies to all future tasks in the fork.
It follows the detailed records in [retrospective #202](https://github.com/lumenpearson/cdpiui/issues/202).
[AGENTS.md](../AGENTS.md) is the canonical agent instruction. [CLAUDE.md](../CLAUDE.md) points Claude to the same rules.

## Create or reuse the owning issue

Search existing issues before implementation. Reuse a matching task and preserve its history.
An independently accepted goal in a large plan can have a linked issue. A commit alone does not need another issue.
Use the [detailed task form](../.github/ISSUE_TEMPLATE/00-task.yml), including for research, documentation, process and verification work.
Defect and audit forms remain intake routes. Before implementation, add any missing task-contract sections without erasing their evidence.

Provide at least 180 words of specific information across these sections:

| Required section | Required information |
|---|---|
| Context and expected result | User scenario, current limitation, source revision and required behavior. |
| Scope and boundaries | Affected responsibilities, compatibility and the exact result this issue accepts. |
| Acceptance criteria | At least two measurable checklist items, with negative or recovery cases when relevant. |
| Verification plan | Commands, environment, review and relevant native checks. Explain a justified source-only method. |
| Risk and recovery | Impact, priority, risk, compatibility and recovery procedure. |
| Dependencies and documentation | Related issues, plans, PRs, source, documentation and blockers. State when none apply. |
| Project classification | The seven classification lines below. |
| Progress and evidence | Execution owner, branch, worktree, initial revision and actual verification state. |

Use `##` headings through the API or `###` headings from issue forms.
The title uses a concrete English imperative subject. Write clear, detailed English with ASD-STE100 principles.
Keep exact identifiers and original authorship. Do not claim formal dictionary certification.
Use specific explanations rather than placeholders. A justified limitation is valid evidence about scope.

## Set actual delivery metadata

Use this classification format in the issue. Replace each value with the result of triage:

```text
Type: type:process
Area: Documentation
Priority: P2
Risk: Low
Gate: G4
Owner: @lumenpearson
Project: https://github.com/users/lumenpearson/projects/7
```

The [governance record](governance/README.md) defines canonical types and areas.
The [delivery gates](delivery-gates.md) define Priority, Risk, Status and G1–G5 scope.
Apply exactly one canonical type label and the relevant area labels. Set the actual assignee and owning milestone.
Add the issue to Project 7. Set Status, Priority, Area and Risk there.
Use Ready after triage and In progress when an execution owner starts work.
The body does not set Project fields. Read back the actual card and record its values.
The form's `type:process` label is an intake default. Replace it for a bug, security task, refactor or other work type.
Dates and estimates can remain unset with an explanation. Never invent historical dates or execution results.

## Maintain the case record

Record decisions, scope changes, failed attempts, resolutions and verification as they occur.
Comments must contain the exact revision, commands, environment, results and material limits.
Link the concrete source, plan, PR and documentation relevant to this case.
Historical tests retain their original revision. Reading test source is not a test run.
If blocked, name the blocker, owner, next action, unblock condition and previous working status.

Link the owning issue from the PR's Related work section. Use `Refs #number` by default.
Keep the issue open until acceptance. Do not let an automatic closing keyword bypass the closing record.
Keep parent integration and release issues open when a scoped child stage finishes.

## Accept and close

Check each acceptance item against current evidence before reporting completion.
For repository changes, record independent review, required checks on the final PR HEAD and the integrated commit.
A source-only method needs a case-specific explanation. Native and installed-product checks remain required for their owning boundaries.
A local stage may close when the contract explicitly accepts that local result. Label its scope and link the pending integration task.

Post a concrete closing comment using this format:

```markdown
## Accepted result
Describe the completed scope and how each acceptance condition was satisfied.

## Revision and integration
Give the full tested commit SHA, branch, PR and integrated SHA when required.
For a local stage, state that it remains local and link the integration issue.

## Checks and results
Give commands, environment, results and evidence links. Separate recorded history from fresh execution.

## Independent review
Give the reviewed SHA, reviewer session or review link, findings and resolutions.
Explain why a review or native check does not apply when the scope does not require it.

## Limits and remaining work
Describe material limits, recovery and follow-up issues. Explain when no remaining work exists in this scope.

## Project readback
Record the actual Project item, Status, Priority, Area, Risk, assignee and milestone.
```

Mark only verified acceptance items complete. Then close the issue with reason `completed` and set Project Status to Done immediately.
Perform both actions in the same working turn. Read back both states and the delivery metadata before claiming completion.
Do not wait for another user reminder when the task is accepted and issue updates are already authorized.
If a write succeeds only partly, repair the remaining metadata and read it again.
If acceptance is incomplete, leave the task open in Review, Verification or Blocked as appropriate.
Reopen a task when a verified regression invalidates its acceptance.

## Automation and access boundaries

The required Contribution metadata status checks for an open owning issue with a detailed contract.
It checks issue labels, assignee and milestone against the classification lines.
Issue edits and metadata changes recheck affected open PRs. The validator re-reads linked issues before publishing a result.
The final read and status write are separate requests. They are not an atomic lock on later edits.
The workflow runs trusted default-branch code. It does not check out or execute PR code with write permissions.

This structural check does not prove semantic correctness, test execution or private Project values.
It does not create issues from chat messages or accept tasks without an executing agent.
Codex and Claude follow the persistent instructions to create, maintain and close issues during each task.
The operator verifies actual Project fields and acceptance evidence.
Use the GitHub API or connector first. Use the authenticated Codex browser when an API cannot complete a required action.
The current repository token can lack private Project access. Do not add a credential or broaden access to avoid that fallback.
Respect the shared exclusive UI queue even for snapshots and read-only browser calls.

## Adopt rules in an active worktree

Apply the same lifecycle to current tasks without taking control of another session's working copy.
Map the branch, worktree, execution owner and owning issue before an update.
An owner can read the published instructions with `git show origin/main:AGENTS.md` and the corresponding document paths.
This applies the rules to the current task without changing its source, index or branch.

On an owner-confirmed safe boundary, add only missing `AGENTS.md`, `CLAUDE.md` and `docs/task-lifecycle.md` from one recorded policy revision.
Verify the exact bytes and preserve the index, HEAD, existing files and all unrelated uncommitted changes.
If a target exists with different content, stop that file update and let its owner resolve it.
Do not stash, reset, pull, rebase, merge, switch branches or create a commit in another active worktree to install these instructions.
The files can remain an explicit local policy overlay until their owner integrates them with the task's normal reviewed change.
An older branch can read missing referenced policy documents from `origin/main` through Git.

A clean worktree with no active execution owner can receive a verified fast-forward from the published main branch.
Check branch identity, cleanliness and the expected old HEAD again immediately before the update.
Keep backup refs and frozen verification worktrees at their recorded identities.
Record each branch's tracked policy, local overlay, context adoption or frozen-snapshot disposition separately.
For existing issues, preserve their original descriptions, criteria and evidence when adding missing contract sections.
Keep task states tied to actual acceptance. Policy adoption does not accept application, native or release work.

## Local validation

```text
python -m unittest discover -s .github/scripts/tests -v
python .github/scripts/check_governance.py
```

The tests cover incomplete contracts, classification mismatch, stale issue changes and quoted example text.
They do not certify that a natural-language acceptance claim is true.
