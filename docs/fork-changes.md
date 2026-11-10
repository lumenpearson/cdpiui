# Fork changes relative to upstream

This record describes `lumenpearson/cdpiui` relative to `Storik4pro/cdpiui`.
The audited upstream revision is `f6f59c6bf5d4bb15a8ce42f8b94c8a0aa7658436`.
The recorded configuration baseline is fork `main` at `d72ad478bc05c6ab9e53f46da1a2b6c58690b202`.
On 10 November 2026, another session replaced the history of `main` with `7ead95f7756057f67cd113671a7f84cf422e8d32`.
Both revisions have tree `33323ba54f948dc7379061e805330bcb08fd610d`.
The file content is equal, but the commit ancestry differs.
Historical commit identifiers below remain evidence pins.
Recheck the live comparison before PR10 integration: the changed history gives it a much larger comparison.

The source and issue links retain the detailed technical records.
Counts in old PR descriptions belong to their recorded revisions.
Do not add the counts from overlapping PRs as if each PR were an independent release.

| Change family | Delivered record | Result and evidence boundary |
|---|---|---|
| Initial stabilization | [PR #2](https://github.com/lumenpearson/cdpiui/pull/2), integrated at `620b8df` | Correctness and trust checks across GUI, tray, Store, presets, add-ons, updater and installer; xUnit, CI and module documentation. Installed-product acceptance has a separate scope. |
| Elevated execution and package trust | [PR #3](https://github.com/lumenpearson/cdpiui/pull/3), merge `952a4e2` | Program approval, argument checks, per-session pipe key, bounded archives, dependency audit and localization. Later audit findings qualify the earlier completion claims. |
| Maintenance and runtime ownership | [PR #4](https://github.com/lumenpearson/cdpiui/pull/4), merge `18f8bc2` | .NET8 tools, download hashes, code leases through process exit, parsing/queue/error corrections and package management. Optional hashes do not establish mandatory Store index trust. |
| Code decomposition and style | [PR #5](https://github.com/lumenpearson/cdpiui/pull/5), merge `6e30da8` | Smaller responsibility units and methods, consistent formatting and regression tests. Preserve stored formats and public behavior. |
| MVVM presentation | [PR #6](https://github.com/lumenpearson/cdpiui/pull/6), merge `05b84d2` | View models, commands and compiled bindings for the major GUI areas. Later fixes and current native acceptance remain separately traceable. |
| Approval and help timeout | [PR #8](https://github.com/lumenpearson/cdpiui/pull/8), merge `36b8cec` | The editor waits through the tray approval window instead of returning an empty option list too early. The PR records its installed-MSI reproduction. |
| Native elevation and CI hardening | [PR #9](https://github.com/lumenpearson/cdpiui/pull/9), merge `d72ad47` | Native AOT launcher, sanitized runtime environment, random pipe names, explicit system-tool paths, proxy credential separation, import limits, secret scan and pinned Actions. |
| File-operation audit | [Draft PR #10](https://github.com/lumenpearson/cdpiui/pull/10), recorded head `ee47716` | Held filesystem objects, protected staging, catalog snapshots, settings and link transactions, bounded cleanup and Store root identity. **Unmerged at the baseline.** Durable recovery, runtime handoff and installed scenarios remain open. |
| Continuing presentation decomposition | [G2 issue #14](https://github.com/lumenpearson/cdpiui/issues/14) | Active isolated work has exact local evidence pins. It is not delivered to `main` until its own PR, review and CI pass. |

[PR #1](https://github.com/lumenpearson/cdpiui/pull/1) closed without merge and was replaced by PR #2.
It is not a second delivered stabilization change.

## Review the changes by boundary

Use [the security index](https://github.com/lumenpearson/cdpiui/issues/21) for source findings, fixes and remaining risk.
Use [G1 acceptance](https://github.com/lumenpearson/cdpiui/issues/13) for PR10 readiness.
Use [installed-product acceptance](https://github.com/lumenpearson/cdpiui/issues/16) for real GUI/tray and system scenarios.
Use [G5](https://github.com/lumenpearson/cdpiui/milestone/5) for candidate qualification and the release decision.

Discussions explain these change families and link to this record.
Issues hold actionable work. Discussions do not replace issue acceptance evidence.

## Discussion index

The pinned [fork change index](https://github.com/lumenpearson/cdpiui/discussions/100) is the entry point.

| Subject | Discussion |
|---|---|
| Initial stabilization | [Behavior, data and trust checks](https://github.com/lumenpearson/cdpiui/discussions/101) |
| Elevated execution and package trust | [Corrections and limits](https://github.com/lumenpearson/cdpiui/discussions/102) |
| Runtime ownership | [Code leases, downloads and recovery](https://github.com/lumenpearson/cdpiui/discussions/103) |
| Decomposition and MVVM | [Observable behavior](https://github.com/lumenpearson/cdpiui/discussions/104) |
| Component approval | [Help request timing](https://github.com/lumenpearson/cdpiui/discussions/105) |
| Native elevation and CI | [Runtime variables and pinned tools](https://github.com/lumenpearson/cdpiui/discussions/106) |
| Draft filesystem audit | [Held objects and open boundaries](https://github.com/lumenpearson/cdpiui/discussions/107) |
| Documentation | [Style and upstream authorship](https://github.com/lumenpearson/cdpiui/discussions/108) |
| Release readiness | [G1–G5 and the go/no-go decision](https://github.com/lumenpearson/cdpiui/discussions/109) |
| Delivery policy | [Ownership, WIP and evidence states](https://github.com/lumenpearson/cdpiui/discussions/110) |
