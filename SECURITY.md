# Security policy

## Scope

This policy applies to the `lumenpearson/cdpiui` fork.
The fork has security changes that differ from `Storik4pro/cdpiui`.
Do not assume that an upstream or fork release contains an unmerged fix.

The project does not claim a supported production release until G5 acceptance is recorded.
Use the exact release notes to identify a qualified version.
The current source branch and a local test fixture are not a support guarantee.

## Report a new vulnerability

Use the repository Security tab to check whether private vulnerability reporting is available.
If it is available, submit the report there.
If it is unavailable, open a minimal issue that asks the maintainer for a private reporting route.
Do not include an exploit, credential, private user data or a sensitive file in that public request.

Include these details in the private report:

- Affected fork and upstream revisions, Windows version and installation type.
- Trust boundary, prerequisites, expected result and actual result.
- A minimal reproduction with disposable data.
- Impact, scope, proposed mitigation and available regression evidence.

The maintainer will triage reports according to impact and available evidence.
Response and fix dates are not guaranteed; record agreed dates in the private report.

## Public historical audit

The owner authorized public tracking of the existing audit findings.
The [audit register](docs/audit/README.md) links findings to issues, commits, tests and remaining acceptance.
A historical source assertion is not automatically a current confirmed defect.
An unmerged fix remains open for integration and verification.
Credential values are excluded from the register.

Revoke a disclosed credential through its provider before considering the exposure contained.
Removing a value from current source does not revoke it or erase public history.
Record provider-side confirmation without copying the value.

## Required boundaries

Keep the GUI at usual-user privileges.
Treat pipe clients, imported presets, archives, Store metadata and writable paths as untrusted inputs.
Verify peer identity, package trust and the exact object used by a privileged operation.
Use bounded operations and preserve recovery data when cleanup ownership is uncertain.
Do not weaken a native guard to make a test pass.

## Release acceptance

Open P0/P1 security blockers prevent release qualification.
Record any lower-risk exception with an owner, mitigation and follow-up issue.
Use current-head CI, independent review and native evidence for the affected boundary.
Installed MSI, portable, live tray and recovery checks remain separate acceptance scopes.
