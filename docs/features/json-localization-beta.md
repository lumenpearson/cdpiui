# Frozen JSON localization beta

The `dev/beta-json-localization` branch preserves JSON localization as a separate experimental feature. Its initial product source is main commit `fe29acfc4238228da3a6aebb98fff4345c3da106`. The owning preservation task is [#235](https://github.com/lumenpearson/cdpiui/issues/235); the stable RESW implementation and active-branch rollout belong to [#236](https://github.com/lumenpearson/cdpiui/issues/236).

This branch retains all 26 EN/RU JSON catalogs, build-time RESW generation for WinUI3Localizer, embedded JSON lookup for the tray and emergency interface, and linked lookup code plus private embedded catalogs for the standalone tools. Its tests and editing instructions remain part of this feature. It adds no unrelated product feature beyond its recorded main baseline.

The stable line is being prepared to use editable RESW catalogs. Both implementations must retain the same current translations, resource identifiers, format arguments, selected-language behavior, live language captions, startup readiness and emergency fallback. These common fixes are not beta-only behavior. A format change must not revert them.

Do not merge this branch automatically into main or another active development branch. To inspect or test the experiment, use a separate checkout and follow [the JSON editing guide](../../Localization/README.md). Preserve any existing settings, installations, occupied worktrees and historical verification artifacts. Branch selection alone does not install a product or accept a release.

The branch is frozen after the scoped preservation checks. Further JSON development requires a new explicit task. Historical `dev/json-localization` and language-caption refs remain attribution/evidence pins; they are not the maintained beta destination.

The final stable SHA, beta SHA and comparison evidence will be recorded in the owning issues after review. Until stable integration and its acceptance actually occur, this document does not claim that main has already switched to RESW.
