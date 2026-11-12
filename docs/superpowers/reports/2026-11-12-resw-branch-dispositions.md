# RESW separation branch dispositions

Owning tasks: #235 (frozen JSON beta) and #236 (stable RESW and active rollout).

## Verified base and current owned work

The initial remote main is `fe29acfc4238228da3a6aebb98fff4345c3da106`. The current-main catalog manifest contains26 catalogs and5330 entries, including20 GUI catalogs/5206 entries. The old primary-checkout test result is not the baseline for this change.

| Branch | Owner-confirmed disposition |
|---|---|
| `dev/beta-json-localization` | New owned JSON preservation destination; beta exception, not an ordinary stable adoption target. |
| `dev/resw-localization` | New owned active stable candidate, initially based on fe29. Tests and review are pending. |
| `main` | Stable adoption target after reviewed PR, native/CI acceptance and restored Project admission. No integration has occurred. |
| `dev/class-stack-integration` at08f6 | Owner01a1c5f4 confirms frozen accepted #14/PR233 evidence. Preserve ref, checkout, index and ignored evidence. |
| `dev/neutral-config-utilities` at249bba | Owner01a1c049 confirms frozen historical/local verification input. Preserve in place. |
| `dev/welcome-migration-presentation` at5c7a945 | Owner/coordinator01a1bc96 confirms frozen published PR232 input, with protected PR230 ancestry. Preserve in place. |
| Future `dev/presentation-owner-integration` | Does not exist yet. Owner01a1c049 will amend #234's baseline and resource/native contract after actual accepted RESW main delivery. It is not updated implicitly. |

## Remaining classification and rollout boundary

The frozen-input receipts are retained in the shared queue and the #235/#236 case records. No frozen PR is retargeted or reported merged merely because its commits are reachable. Earlier policy/decomposition/localization refs and backups remain historical until their actual owners/activity are fully reconciled; a branch prefix alone does not establish active work.

The primary checkout remains on local main7ead95f with preserved localization WIP. Its index hash was captured before work. It must not receive a reset, stash, silent history move or whole-tree overwrite. Any later owner-controlled source cleanup must preserve unrelated staged/unstaged and ignored files and prove that moved localization work exists in the beta/evidence.

Actual Project7 access is blocked by the existing browser-origin policy. The user explicitly allows local code/branch/build/review preparation despite that metadata prerequisite. This exception does not authorize a browser workaround, a native UI lease, main integration or completed/Done closure without the remaining acceptance. Shared UI owner.json remains sole-writer coordinator state; this actor has no write authority there.

This is an in-progress ledger. Final source, branch, test, review, artifact and integration evidence must replace pending statements before either issue is accepted.
