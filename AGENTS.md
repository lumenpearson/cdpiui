# Fork task delivery rules

These rules apply to every future task in `lumenpearson/cdpiui` and its linked worktrees.
Read [the task lifecycle](docs/task-lifecycle.md), [engineering rules](docs/engineering.md) and [delivery gates](docs/delivery-gates.md) before implementation.
User instructions take precedence. Preserve the scope of the user's authorization.

If a required policy document is absent in an older branch, read its published version with `git show origin/main:<path>`.
This read does not require a checkout, merge or index change. Use the owner-controlled adoption procedure in the task lifecycle.

## Start every task with an issue

1. Search open and closed issues for the same goal. Reuse the owning issue when its scope matches.
2. Otherwise, create a detailed issue before implementation. Include investigation, documentation, verification and process work.
3. Use all eight sections from the [task form](.github/ISSUE_TEMPLATE/00-task.yml). Give case-specific information and measurable acceptance criteria.
4. Apply exactly one canonical `type:` label and the relevant canonical `area:` labels. Assign the owner and G1–G5 milestone.
5. Add the issue to [Project 7](https://github.com/users/lumenpearson/projects/7). Set Status, Priority, Area and Risk.
6. Read back the actual issue and Project fields. A classification paragraph is not proof that Project fields were set.
7. Record the execution owner, branch, worktree, dependencies and verification plan. Use `dev/<purpose>` for a new branch.

Do not create a second issue for a status question or clarification within the same task.
For a plan with independently accepted goals, create linked issues for those goals. Do not create one issue per commit.
Existing issues may receive an additive task contract. Preserve their historical description, comments, attribution and evidence.

## Keep evidence current

Use clear English titles and detailed English prose. Apply ASD-STE100 writing principles and preserve exact technical identifiers.
Update the issue when scope, dependencies, decisions, revisions or verification change.
Add progress comments with commands, results, environment, source and documentation links, and material limits.
Keep failed attempts and their resolutions visible. Distinguish inspected source from executed tests and historical results.
Keep issue and PR labels, owner, milestone and Project fields consistent with the actual scope.
Use `Refs #number` in a PR by default. An automatic closing keyword must not bypass issue acceptance.

## Accept and close immediately

Before reporting a task as complete:

1. Check every acceptance item against current evidence. Run the appropriate checks on the named revision.
2. For repository changes, obtain independent review and verify required checks on the final PR HEAD. Record integration when the scope requires it.
3. Complete relevant native or installed-product verification. Explain why a source-only check is sufficient when it is used.
4. Update documentation and record remaining risks or linked follow-ups. Keep a broad integration or release issue open when its criteria remain incomplete.
5. Add the [case-specific acceptance record](docs/task-lifecycle.md#accept-and-close) to the issue. Mark only verified criteria complete.
6. Immediately close the issue as `completed` and set its Project Status to `Done` in the same working turn.
7. Read back closure reason, labels, milestone, assignee and Project fields. Correct a partial metadata update before claiming completion.

Do not defer an accepted issue for another reminder or ask again for routine issue updates within the authorized task.
Do not close a task because a commit exists, a PR merged, CI passed, or a project counter increased.
A local stage may close only when its contract explicitly accepts a local result. Record that integration and release acceptance remain separate.
If acceptance fails, leave the issue open in the appropriate working state. Reopen it when a verified regression invalidates acceptance.

## Access and verification

Use the GitHub API or connector first. If it cannot read or write a required field, use the authenticated Codex browser.
For private Project 7, an Actions token can lack Project access. Do not claim that the CI metadata check verifies private Project fields.
Do not add credentials or widen access merely to avoid the browser fallback.
Respect the shared exclusive UI queue, including snapshots and read-only UI calls.
Never publish credentials, private user data, complete logs with secrets, or fabricated execution results.
The task contract validator checks structure and issue metadata. The agent remains responsible for accurate content and semantic acceptance.
