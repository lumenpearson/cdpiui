# Security audit register

This register maps historical findings to current tracking issues.
It does not assert that every old defect still exists in the fork.

## Source and acceptance boundaries

The retained original audit has 1,674 lines.
Its SHA-256 is `EE7516160403ECE00373A19D9327E0567F5F37457CD1DB92A872750C8E370194`.
The bytes match the `AUDIT-2.md` digest recorded in the [continuation journal](https://github.com/lumenpearson/cdpiui/blob/ee4771675956fe213634675f37579362efb7e0eb/docs/AUDIT-STATUS.ru.md).
The complete credential-section body is excluded. Credential values are never published or tested.

The original analysis was static. Its check marks indicate source review, not an executed PoC.
Each issue distinguishes a historical assertion, an uncertain candidate and a proposed acceptance procedure.
Current severity requires current impact evidence; source ratings remain historical.

Baseline `main`: `d72ad478bc05c6ab9e53f46da1a2b6c58690b202`.
Draft PR10 head: `ee4771675956fe213634675f37579362efb7e0eb`.
Fixes found only in PR10 remain unmerged.
A regression source link is not a new test execution record.

## Coverage

- 59 original-source rows: 39 historical findings and 20 uncertain boundary candidates.
- 12 later file-operation findings, FS-01–FS-12.
- 7 independent historical fork-review mechanisms, including three dependency cases.
- 78 independent issues in total. Repeated source references use the alias map.

The [machine-readable register](findings.json) contains root causes, source ranges, reproduction procedures, exact commits and regression-file links.
Use [security index #21](https://github.com/lumenpearson/cdpiui/issues/21) for ongoing acceptance.
Use [delivery gates](../delivery-gates.md) for integration and release criteria.

## Findings

| ID | Tracking issue | Source classification | Implementation and acceptance |
|---|---|---|---|
| `C1` | [#22: Untrusted peers can request arbitrary elevated process launches](https://github.com/lumenpearson/cdpiui/issues/22) | Historical | Main restricts peer access and elevated launches. See the issue for the remaining scope. |
| `C2` | [#23: GrantAccess permits arbitrary elevated ACL changes](https://github.com/lumenpearson/cdpiui/issues/23) | Historical | The ACL command remains scoped rather than removed. See the issue for the remaining scope. |
| `C3` | [#24: Elevated installer accepts unchecked or replaceable packages and executable fallback](https://github.com/lumenpearson/cdpiui/issues/24) | Historical | Signature validation, expected SHA256 and protected staging replace caller-selected package trust. See the issue for the remaining scope. |
| `H1` | [#25: Privileged launches consume user-writable executables](https://github.com/lumenpearson/cdpiui/issues/25) | Historical | Main uses fingerprint approval and code-file leases. See the issue for the remaining scope. |
| `H2` | [#26: Autorun persists trust in writable installation and component paths](https://github.com/lumenpearson/cdpiui/issues/26) | Historical | PR10 checks owners, DACLs and complete MSI/portable path chains. See the issue for the remaining scope. |
| `H3` | [#27: Pipe squatting exposes authentication and impersonation](https://github.com/lumenpearson/cdpiui/issues/27) | Historical | Main uses a random per-start pipe name, first-instance behavior, retries and peer validation. See the issue for the remaining scope. |
| `M1` | [#28: Elevated log opening trusts per-user shell associations](https://github.com/lumenpearson/cdpiui/issues/28) | Historical | Main starts shell tools by full system paths and uses desktop-user launch paths. See the issue for the remaining scope. |
| `M2` | [#29: ReloadTasks creates caller-selected directories elevated](https://github.com/lumenpearson/cdpiui/issues/29) | Historical | PR10 removes elevated task-directory creation; the GUI creates its own directory. See the issue for the remaining scope. |
| `M3` | [#30: Internal XML entities can consume elevated parser resources](https://github.com/lumenpearson/cdpiui/issues/30) | Candidate | DTD restrictions and resource limits are implemented. See the issue for the remaining scope. |
| `2-auth-guid-logging` | [#31: Authentication identifier reaches application/debug logs](https://github.com/lumenpearson/cdpiui/issues/31) | Historical | Main removes authentication identifiers from logs and uses per-session credentials. See the issue for the remaining scope. |
| `S1` | [#32: Common and repository ZIP extraction escape destination](https://github.com/lumenpearson/cdpiui/issues/32) | Historical | Main validates ZIP paths. See the issue for the remaining scope. |
| `S2` | [#33: Elevated updater extraction escapes installation root](https://github.com/lumenpearson/cdpiui/issues/33) | Historical | Main validates update entry paths and bounds archives. See the issue for the remaining scope. |
| `S3` | [#34: Catalog checks identity without signature validity](https://github.com/lumenpearson/cdpiui/issues/34) | Historical | Main verifies cryptographic signatures and signer pins. See the issue for the remaining scope. |
| `S3-powershell-path-injection` | [#35: Catalog checker embeds an unescaped path in PowerShell](https://github.com/lumenpearson/cdpiui/issues/35) | Historical | The verifier uses encoded PowerShell command input and escaped literal paths. See the issue for the remaining scope. |
| `S3-verifier-process-bounds` | [#36: Catalog verifier subprocess can block without bounds](https://github.com/lumenpearson/cdpiui/issues/36) | Candidate | Main defines a two-minute catalog file-check timeout. See the issue for the remaining scope. |
| `S4` | [#37: Unsigned packages restore executable actions after sanitization](https://github.com/lumenpearson/cdpiui/issues/37) | Historical | Main limits unsigned actions and imported write options. See the issue for the remaining scope. |
| `S5` | [#38: Store lacks end-to-end authenticated component integrity](https://github.com/lumenpearson/cdpiui/issues/38) | Historical | HTTPS and optional SHA256 exist. See the issue for the remaining scope. |
| `3-embedded-client-credentials` | [#39: Distributed credentials cannot remain confidential](https://github.com/lumenpearson/cdpiui/issues/39) | Historical | Distributed client credentials cannot be confidential. See the issue for the remaining scope. |
| `3-proxy-token-disclosure` | [#40: Store forwards provider credentials to proxy](https://github.com/lumenpearson/cdpiui/issues/40) | Historical | Main forwards the provider token only to its provider host. See the issue for the remaining scope. |
| `3-api-version-path-traversal` | [#41: Preferred version changes authenticated API path](https://github.com/lumenpearson/cdpiui/issues/41) | Historical | APIWorker escapes version, owner and repository path components with Uri.EscapeDataString. See the issue for the remaining scope. |
| `3-unsigned-update-requirements` | [#42: Update control metadata lies outside catalog authentication](https://github.com/lumenpearson/cdpiui/issues/42) | Candidate | Catalog/package trust checks precede privileged installation. See the issue for the remaining scope. |
| `3-storeid-delete-traversal` | [#43: Unvalidated StoreId chooses recursive deletion path](https://github.com/lumenpearson/cdpiui/issues/43) | Historical | Main validates IDs. See the issue for the remaining scope. |
| `3-fixed-repository-tempfile` | [#44: Repository loader reuses predictable temporary path](https://github.com/lumenpearson/cdpiui/issues/44) | Candidate | Unique temporary names replace predictable shared paths. See the issue for the remaining scope. |
| `4-pipe-payload-encoding` | [#45: Pipe serialization does not encode field delimiters](https://github.com/lumenpearson/cdpiui/issues/45) | Candidate | Main encodes serialized field delimiters. See the issue for the remaining scope. |
| `4-pipe-framing-failure` | [#46: Pipe framing and disconnect errors escape handling](https://github.com/lumenpearson/cdpiui/issues/46) | Candidate | Main handles broken disposal. See the issue for the remaining scope. |
| `6-workflow-script-injection` | [#47: Release expressions enter PowerShell code](https://github.com/lumenpearson/cdpiui/issues/47) | Historical | Release workflow handling and PowerShell invocation were rewritten. See the issue for the remaining scope. |
| `6-unverified-update-download` | [#48: Workflow downloads release input without transport/digest verification](https://github.com/lumenpearson/cdpiui/issues/48) | Historical | Build and release scripts were rewritten. See the issue for the remaining scope. |
| `6-unpinned-actions` | [#49: Workflow uses mutable outdated third-party actions](https://github.com/lumenpearson/cdpiui/issues/49) | Historical | Main pins Actions to full commit SHAs. See the issue for the remaining scope. |
| `9.1` | [#50: External URI dispatches internal messages and reflective window types](https://github.com/lumenpearson/cdpiui/issues/50) | Historical | Main restricts public activation routes and internal window types. See the issue for the remaining scope. |
| `10.4` | [#51: LScript download filename escapes item root](https://github.com/lumenpearson/cdpiui/issues/51) | Historical | Main validates download leaves and sources, including LScript filenames. See the issue for the remaining scope. |
| `10.9` | [#52: Rejected package bytes remain in installed-item directory](https://github.com/lumenpearson/cdpiui/issues/52) | Candidate | Store changes isolate failed first installs and preserve the working version. See the issue for the remaining scope. |
| `10-database-path-prefix` | [#53: Path restoration checks a lexical prefix without boundary](https://github.com/lumenpearson/cdpiui/issues/53) | Candidate | Main checks item path prefixes with a separator boundary. See the issue for the remaining scope. |
| `10-dynamicimage-local-file-copy` | [#54: Package preview copies arbitrary absolute local file](https://github.com/lumenpearson/cdpiui/issues/54) | Historical | Local pack icon handling was corrected. See the issue for the remaining scope. |
| `11.7-export-traversal` | [#55: Package export writes through parent-relative resource paths](https://github.com/lumenpearson/cdpiui/issues/55) | Historical | Main bounds export copies to the pack and reports failed exports. See the issue for the remaining scope. |
| `12.10` | [#56: Unauthenticated pipe peers receive component output and paths](https://github.com/lumenpearson/cdpiui/issues/56) | Historical | Main sends component output only to an authorized client. See the issue for the remaining scope. |
| `12.10-idle-connection-DoS` | [#57: Single idle unauthenticated pipe connection denies legitimate clients](https://github.com/lumenpearson/cdpiui/issues/57) | Historical | Main drops idle unauthorized clients. See the issue for the remaining scope. |
| `14.4` | [#58: Imported arguments request privileged component file writes](https://github.com/lumenpearson/cdpiui/issues/58) | Historical | Main restricts imported write-producing component arguments. See the issue for the remaining scope. |
| `14.12-archive-expansion` | [#59: ConfigShare expands archive before consent without ratio limits](https://github.com/lumenpearson/cdpiui/issues/59) | Historical | Archive bounds are implemented. See the issue for the remaining scope. |
| `14.12-quadratic-parser` | [#60: ConfigShare quote scanning has quadratic cost](https://github.com/lumenpearson/cdpiui/issues/60) | Historical | ConfigShare parsing and limits were hardened. See the issue for the remaining scope. |
| `14.12-resource-memory` | [#61: ConfigShare allocates large per-resource buffers](https://github.com/lumenpearson/cdpiui/issues/61) | Historical | Resource and archive bounds are implemented. See the issue for the remaining scope. |
| `14-import-local-file-disclosure` | [#62: Batch import copies arbitrary absolute files into exportable preset](https://github.com/lumenpearson/cdpiui/issues/62) | Historical | Main refuses files outside the import source. See the issue for the remaining scope. |
| `14-goodcheck-argument-validation` | [#63: Unchecked passesValue enters GoodCheck arguments](https://github.com/lumenpearson/cdpiui/issues/63) | Candidate | Elevated argument validation exists, but the source passesValue consequence is a candidate. See the issue for the remaining scope. |
| `20-credential-history` | [#64: Credential exposed in public Git history](https://github.com/lumenpearson/cdpiui/issues/64) | Historical | Gitleaks scans current files and new commits. See the issue for the remaining scope. |
| `22.1-cross-account-gui-environment` | [#65: GUI inherits elevated-account environment](https://github.com/lumenpearson/cdpiui/issues/65) | Candidate | Main reads the desktop user settings and has separate desktop-user GUI launch logic. See the issue for the remaining scope. |
| `22.1-gui-activation-arguments` | [#66: Tray embeds unquoted GUI activation arguments](https://github.com/lumenpearson/cdpiui/issues/66) | Candidate | Main preserves GUI activation arguments across tray startup. See the issue for the remaining scope. |
| `22.2-unbounded-component-output` | [#67: Component output buffers grow indefinitely](https://github.com/lumenpearson/cdpiui/issues/67) | Candidate | Output processing was corrected. See the issue for the remaining scope. |
| `22.3-predictable-proxy-secret` | [#68: TgWsProxy secret uses non-cryptographic PRNG](https://github.com/lumenpearson/cdpiui/issues/68) | Historical | Main uses a cryptographic generator for random hex values. See the issue for the remaining scope. |
| `22.4-report-import-memory` | [#69: Report import permits multiplicative memory exhaustion](https://github.com/lumenpearson/cdpiui/issues/69) | Historical | Main lowers report import limits and validates report inputs. See the issue for the remaining scope. |
| `22.5-malformed-report-ui-crash` | [#70: Malformed report error path violates UI thread affinity](https://github.com/lumenpearson/cdpiui/issues/70) | Candidate | Main routes asynchronous UI work correctly in changed pages. See the issue for the remaining scope. |
| `22.6-config-name-path` | [#71: Config name controls directory creation before acceptance](https://github.com/lumenpearson/cdpiui/issues/71) | Candidate | GUI controls and storage helpers were corrected and later decomposed. See the issue for the remaining scope. |
| `23-pipe-busy-loop` | [#72: Pipe error handling can busy-loop](https://github.com/lumenpearson/cdpiui/issues/72) | Candidate | Pipe lifecycle changes handle broken reads and cancellation. See the issue for the remaining scope. |
| `23-pipe-malformed-message` | [#73: Malformed pipe fields can raise unhandled lookup errors](https://github.com/lumenpearson/cdpiui/issues/73) | Candidate | Key parsing skips group markers and codec handling is tested. See the issue for the remaining scope. |
| `23-hostname-newline-validation` | [#74: Hostname regex permits trailing newline](https://github.com/lumenpearson/cdpiui/issues/74) | Candidate | BlockCheck target parsing was corrected. See the issue for the remaining scope. |
| `23-curl-binary-planting` | [#75: Preset test resolves unqualified curl.exe](https://github.com/lumenpearson/cdpiui/issues/75) | Historical | Main resolves curl through the full system path. See the issue for the remaining scope. |
| `23-proxy-port-validation` | [#76: Proxy port input lacks validation](https://github.com/lumenpearson/cdpiui/issues/76) | Candidate | Main validates proxy port input. See the issue for the remaining scope. |
| `23-unrestricted-link-schemes` | [#77: License and Store links open arbitrary URI schemes](https://github.com/lumenpearson/cdpiui/issues/77) | Historical | Main restricts external links to documented schemes. See the issue for the remaining scope. |
| `23-goodcheck-quadratic-log` | [#78: GoodCheck log concatenation has quadratic cost](https://github.com/lumenpearson/cdpiui/issues/78) | Candidate | The GoodCheck log view was corrected. See the issue for the remaining scope. |
| `23-installer-elevated-gui` | [#79: Installer launches full GUI elevated including quiet/repair](https://github.com/lumenpearson/cdpiui/issues/79) | Historical | Main avoids elevated GUI launch and suppresses launch after silent installation. See the issue for the remaining scope. |
| `23-ci-single-string-command` | [#80: Workflow invokes pwsh using one constructed command string](https://github.com/lumenpearson/cdpiui/issues/80) | Candidate | Patch and release scripts require PowerShell7 and use revised commands. See the issue for the remaining scope. |
| `FS-01` | [#81: Updater destination links escape the selected tree](https://github.com/lumenpearson/cdpiui/issues/81) | Later file audit | PR10 validates and holds destination/staging/backup objects. See the issue for the remaining scope. |
| `FS-02` | [#82: A new update removes the only retained recovery backup](https://github.com/lumenpearson/cdpiui/issues/82) | Later file audit | PR10 preserves an existing nonempty backup and refuses unsafe cleanup instead of deleting the only original.. See the issue for the remaining scope. |
| `FS-03` | [#83: Text merge follows an unsafe selected backup path](https://github.com/lumenpearson/cdpiui/issues/83) | Later file audit | PR10 checks the selected backup and uses native no-follow write/append operations. See the issue for the remaining scope. |
| `FS-04` | [#84: Settings writes proceed after lock acquisition fails](https://github.com/lumenpearson/cdpiui/issues/84) | Later file audit | PR10 requires a real lock object before disk writes. See the issue for the remaining scope. |
| `FS-05` | [#85: Failed link publication loses the displaced original](https://github.com/lumenpearson/cdpiui/issues/85) | Later file audit | PR10 retains originals and backups through publication and rollback. See the issue for the remaining scope. |
| `FS-06` | [#86: Failed link restoration removes the working link first](https://github.com/lumenpearson/cdpiui/issues/86) | Later file audit | PR10 retains and restores the current link when backup promotion fails. See the issue for the remaining scope. |
| `FS-07` | [#87: Staged package trust expires before the consumer finishes](https://github.com/lumenpearson/cdpiui/issues/87) | Later file audit | PR10 uses admin-owned staging and consumer leases. See the issue for the remaining scope. |
| `FS-08` | [#88: Signed package verification and publication consume different files](https://github.com/lumenpearson/cdpiui/issues/88) | Later file audit | PR10 holds the exact source set and verifies catalog membership against it. See the issue for the remaining scope. |
| `FS-09` | [#89: Mutable destination paths redirect writes or truncate linked files](https://github.com/lumenpearson/cdpiui/issues/89) | Later file audit | PR10 uses relative native handles, guarded creation and held parents. See the issue for the remaining scope. |
| `FS-10` | [#90: Settings lock, recovery and backup paths can be substituted](https://github.com/lumenpearson/cdpiui/issues/90) | Later file audit | PR10 holds settings objects and publishes atomically with preserved access rules. See the issue for the remaining scope. |
| `FS-11` | [#91: Multi-file state lacks durable recovery across interruption](https://github.com/lumenpearson/cdpiui/issues/91) | Later file audit | PR10 serializes cooperating updaters and rolls back batch publication on exceptions. See the issue for the remaining scope. |
| `FS-12` | [#92: Recursive cleanup and directory replacement lose ownership](https://github.com/lumenpearson/cdpiui/issues/92) | Later file audit | PR10 adds bounded no-follow removal, exact Store root/ID binding and held directory replacement. See the issue for the remaining scope. |
| `SEC-ENV` | [#93: User runtime variables reach elevated managed programs](https://github.com/lumenpearson/cdpiui/issues/93) | Historical | Native AOT launcher removes DOTNET/CORECLR/COR/COREHOST/COMPlus variables and uses explicit targets. See the issue for the remaining scope. |
| `SEC-LEASE` | [#94: Code-file leases are released before lazy DLL loading ends](https://github.com/lumenpearson/cdpiui/issues/94) | Historical | Main retains executable and library leases until component/help/GoodCheck exit. See the issue for the remaining scope. |
| `DEP-PKCS` | [#95: Historical vulnerable PKCS dependency](https://github.com/lumenpearson/cdpiui/issues/95) | Historical | Main pins System.Security.Cryptography.Pkcs 8.0.2 instead of the historical 8.0.1. See the issue for the remaining scope. |
| `DEP-SQLITE` | [#96: Historical vulnerable native SQLite dependency](https://github.com/lumenpearson/cdpiui/issues/96) | Historical | Main pins SQLitePCLRaw.lib.e_sqlite3 2.1.14 instead of the historical 2.1.11. See the issue for the remaining scope. |
| `DEP-DRAWING` | [#97: Historical vulnerable System.Drawing dependency](https://github.com/lumenpearson/cdpiui/issues/97) | Historical | Main pins System.Drawing.Common 8.0.31 instead of the historical 4.7.0. See the issue for the remaining scope. |
| `SEC-PIPE-TIMING` | [#98: Historical pipe credential comparison leaks timing](https://github.com/lumenpearson/cdpiui/issues/98) | Historical | Main compares credentials in fixed time. See the issue for the remaining scope. |
| `SEC-LISTEN` | [#99: Proxy component listens beyond the expected local interface](https://github.com/lumenpearson/cdpiui/issues/99) | Historical | Main asks before a proxy component listens on a network address. See the issue for the remaining scope. |

## Close a finding

1. Reconcile the source claim against the current code.
2. Record confirmed, disproved, duplicate or residual-risk status.
3. Link the integrated fix and appropriate regression evidence.
4. Record current-head CI, independent review and native limits.
5. Keep installed-product, durable recovery and release acceptance separate.

Source integrity, authenticated package metadata, runtime handoff and cleanup ownership have different contracts.
Do not close all of them from one green build or one completed PR.
Provider-side credential revocation remains an owner verification task until evidence exists.
