# ConfigMaker Decomposition Implementation Plan

Возобновлён 9 ноября 2026 по прямому запросу пользователя без временного ограничения. Дедлайн и winddown ниже относятся к завершённому первому окну; новые этапы начинаются с Task39 на product e42 / docs63ce869. Остальные ограничения безопасной автономной работы сохраняются.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Extract tree construction and resource editing while retaining current editor behavior.
**Architecture:** Pure tree data and builder, a WinUI adapter, resource registry and reference editor; existing facade/ViewModel APIs stay compatible.
**Tech Stack:** C#, .NET 8, WinUI 3, xUnit; no additional packages.
**Spec:** ../specs/2026-11-08-class-decomposition-design.md

## Global Constraints

- Baseline f39e8a0; other session owns security/staging/Update/ZIP/settings.
- No changes to preset formats, localization, XAML bindings or UI behavior.
- Preserve aliases, direct variable paths, Lua markers, blob offsets, profile names and source spans.
- Add test source links without replacing existing project entries.
- User requires no regressions: verify tests and GUI build before completion.
- For all further passes, prefer small utility objects with a neutral purpose when the module's
  architecture provides a real boundary. Separate reusable mechanisms from UI/domain orchestration;
  do not merely move a coupled large host into another large specialized class. Do not invent generic
  abstractions, extra interfaces or claimed reuse without a concrete need. Small domain owners and
  WinUI adapters remain appropriate where they preserve coherent state and observable behavior.

## Review Focus

- File categories, current-culture sorting and missing/attached file tooltips.
- Empty or marker-only profile fallback and source spans.
- Remove a new resource if neither command nor variables actually change.
- Reuse existing resources for repeated case-insensitive paths.
- Direct paths and command snippets in Value/Values/OnValue/OffValue.

### Task 1: Pure tree builder and UI adapter

**Files:** helpers ConfigMakerPresetTreeBuilder.cs, ConfigMakerPresetTreeData.cs, ConfigMakerPresetTreeAdapter.cs; ViewModel.PresetStructure.cs; Presentation/ConfigMakerPresetTreeBuilderTests.cs; additive test source links.
**Interfaces:** BuildFiles(files) returns IReadOnlyList<ConfigMakerPresetFileNode>; BuildGroups(options, explicitGroups, isZapret2) returns IReadOnlyList<ConfigMakerPresetOptionNode>. Constructor takes localize, resolvePath, fileExists and describeOption functions. Adapter returns existing ConfigMakerTreeNode objects.

- [x] Write tests for categories/sorting/tooltips, empty input, common groups, multiple profiles, name precedence, fallback, descriptions and source spans.
- [x] Run tests against the absent API, then extract the existing algorithms and make tests green.
- [x] Wire builder/adapter while retaining ViewModel notification order; run GUI x64 build.

### Task 2: Resource registry and reference editor

**Files:** helpers ConfigMakerPresetResourceRegistry.cs, ConfigMakerPresetReferenceEditor.cs, ConfigMakerPresetResources.cs; Presentation resource/reference tests; additive source links.
**Interfaces:** Registry takes IList<ConfigMakerPresetResource>, resolvePath, isInsideComponent; exposes FindAttached, CreateUniqueAlias, AddFiles, GetOrCreate overloads, TryUpdateAttachedResource, Remove. ReferenceEditor takes variables, registry, extractFiles, extractDirectFile; exposes AttachExistingFiles(command) and ReplaceReferences(command,file,replacementPath).

- [x] Test case-insensitive reuse/aliases, Cyrillic, built-in flags, missing files, all variable values, direct/inline paths, blob/Lua and no-op rollback.
- [x] Run tests before classes exist; extract the original algorithms without changing policy; retain the facade API.
- [x] Run targeted tests, full xUnit suite and GUI x64 build.

### Task 3: Independent review and evidence

- [x] Compare old/new algorithms and notification order with a fresh reviewer.
- [x] Reproduce any actionable defect in a failing test before fixing.
- [x] Final full xUnit suite, GUI build and git diff --check; record class sizes and limitations.
- [x] Report the isolated diff and next classes without claiming the audit complete.

## Execution

User approved autonomous continuation. Execute inline with one final reviewer, without per-task approvals.
User additionally authorized safe local checkpoints and autonomous acceptance until 14:00 MSK on 2026-11-09 (supersedes 02:00). Use deadline-guarded checkpoint.ps1; no deletion/publish/push/merge/reset. Scratch progress: .superpowers/sdd/2026-11-08-class-decomposition.

### Task 4: App activation dispatcher

**Files:** Helper/Basic/AppActivationRequest.cs, AppActivationDispatcher.cs, AppActivationRequestAdapter.cs; App.xaml.cs and App.Activation.cs; Presentation/AppActivationDispatcherTests.cs; additive linked test sources.
**Interfaces:** ProcessStartupAsync(string[] arguments, Func<AppActivationRequest> getInitialActivation) and ProcessRedirectedAsync(AppActivationRequest). Constructor takes migration-argv, migration-launch, files, protocol, direct, splitter and startup-error callbacks.

- [x] Write sequence/fallback/partial-failure/await/payload tests before extracting.
- [x] Move orchestration only, preserving callback timing, exceptions and inherited precedence.
- [x] Run targeted/full tests and GUI build; review independently before completion.

### Task 5: BlockCheck2 options ownership

**Files:** ViewModels/BlockCheck2OptionsViewModel.cs, IBlockCheck2OptionsSettings.cs, BlockCheck2OptionsSettings.cs; existing BlockCheck2ViewModel partials; Presentation/BlockCheck2OptionsTests.cs and source links.
**Interfaces:** Options constructor accepts typed settings, Func<bool> isRunning and Func<int> selectedSiteListCount; properties retain names/types; CreateSessionRequest(Func<IReadOnlyList<BlockCheckSiteListInput>>) retains evaluation order.

- [x] Write behavioral tests for defaults, corrupted values, derived validity, recommendations, notifications/save order and request snapshots.
- [x] Transfer state/options logic; forward host properties/events and keep process execution unchanged.
- [x] Targeted/full tests, GUI build and independent read-only comparison with baseline.

### Task 6: ConfigMaker command-help panel

**Files:** Helper/CreateConfigHelper/ConfigMakerCommandHelpPanel.cs; ConfigMakerViewModel.CommandHelp.cs/.cs/.Diagnostics.cs; Presentation/ConfigMakerCommandHelpPanelTests.cs and additive production source links.
**Interfaces:** Panel constructor Func<string,string> localize, Func<bool> isEditorReadOnly; Document and stable CommandOptions/CommandModules; existing selection/search/argument properties; SelectCommand, RebuildCommandModules, RebuildCommandOptions, BuildSelectedCommand. INotifyPropertyChanged is forwarded by host; async loading/status/clipboard/editor remain outside.

- [x] Test module/query/reload/selection/argument/notification/reentrancy contracts against absent panel API.
- [x] Extract existing state and algorithms, retain public host proxies and command notification order.
- [x] Full tests, GUI and actual adapter smoke; independent review vs bdc9170; fix any regression before local checkpoint.

### Task 7: ConfigMaker test-session lifecycle

**Files:** helpers ConfigMakerTestSession.cs, IConfigMakerTestTasks.cs, ConfigMakerComponentTestTasks.cs; ConfigMakerViewModel.Testing.cs/.cs; Presentation/ConfigMakerTestSessionTests.cs and source links.
**Interfaces:** Session constructor: tasks, Func<string> currentComponent, Func<Task<bool>> confirm, Func<string,bool> installed, Action<ConfigMakerTestNotice,string?> notice, Func<Task> refresh, Action<Func<Task>> enqueue; StartAsync(arguments), StopAsync(showCompletionMessage=false), IsTesting, TestComponentId, StateChanged.

- [x] Test observable process/notice/state order and both failure boundaries before implementing.
- [x] Extract lifecycle; preserve all host APIs, actual process manager methods and live-read timing.
- [x] Full test/GUI run, independent comparison with pre-task checkpoint, local commit after checks.

### Task 8: ConfigMaker simple-designer ownership

**Files:** helpers ConfigMakerSimpleDesigner.cs, IConfigMakerSimpleDesignerBackend.cs, ConfigMakerSimpleDesignerBackend.cs; existing SimpleDesigner partial/.cs; Presentation/ConfigMakerSimpleDesignerTests.cs; pure model/base/formatter source links.
**Interfaces:** backend IsSupported, TryLoad, Parse, Build; owner constructor backend/currentComponent/editor getter/setter/three ICommand/notice callback. Owner keeps named collections/properties, MarkDirty, UpdateAvailability, OnEditorViewChanged, OnAdditionalDesignerArgumentsChanged, three change handlers.

- [x] Test loading/selection/editing/reentrancy/dirty/error contracts before implementation.
- [x] Transfer existing state and algorithms; retain exact production backend and host command identity.
- [x] Full tests, GUI, independent review vs70b4295 and safe local checkpoint.

### Task 9: App window lookup and creation ownership

**Files:** Helper/WindowHelper/AppWindowCoordinator.cs, IAppWindowOperations.cs, AppWindowOperations.cs; App.xaml.cs/App.Windows.cs; Presentation/AppWindowCoordinatorTests.cs and additive source links.
**Interfaces:** coordinator receives existing List<TWindow>, Func<Task> readiness and operations backend; GetCurrent(Type), CloseAll(Type), CloseTemplate(Type,id), CreateSafeAsync(Type,activate), CreateUnsafeAsync(Type,activate,id). Backend retains actual WinUI APIs and host RegisterWindow.

- [x] Write tests for readiness/snapshots, reuse/duplicate/dead windows, id matching, close ordering and failure propagation before implementation.
- [x] Transfer rules, preserve public App API/list and event lifecycle boundaries.
- [x] Full suite, GUI, independent review vs b4593ee; safe local checkpoint.


### Task 10: BlockCheck2 displayed progress ownership

**Files:** ViewModels/BlockCheck2ProgressViewModel.cs, IBlockCheck2ProgressEstimator.cs; existing estimator and BlockCheck2ViewModel.cs; Presentation/BlockCheck2ProgressTests.cs and source links.
**Interfaces:** owner receives live targets/validation-attempts/startup-grace providers plus optional existing estimator implementation; Update, StartRun, FinishRun, Reset(beforeNotifications), CompleteDisplayedProgress. Host forwards properties/events and retains result/state/cancellation/history lifecycle.

- [x] Test displayed progress/target formatting, estimator arguments and sequencing, update/reset/finally notifications and repeated/reentrant reads before implementation.
- [x] Transfer fields and presentation algorithms without changing estimator math, report/state/cancellation ordering or source-of-truth list.
- [x] Full suite and GUI, independent review vs ecc7828, then reversible local checkpoint.

### Task 11: ConfigMaker help-loading lifecycle

**Files:** Helper/CreateConfigHelper/ConfigMakerCommandHelpLoader.cs; host CommandHelp.cs/.cs; Presentation/ConfigMakerCommandHelpLoaderTests.cs and source link.
**Interfaces:** loader receives existing panel, live component/localizer functions, production LoadAsync delegate and structure/diagnostics/status callbacks; LoadAsync(componentId,forceRefresh), Cancel, IsHelpLoading/IsCommandListEnabled/UsageText with host event forwarding.

- [x] Test concurrent/canceled/stale results, notification/callback order and exception/reentrancy boundaries before implementation.
- [x] Move lifecycle unchanged; keep production service/parser/cache, host public APIs and cancel-then-timer order.
- [x] Full tests and GUI, independent comparison with b12d857, safe local checkpoint.

### Task 12: Fixture-backed live WinUI verification

**Artifacts:** ignored scratch harness/patch scripts, a pinned short verification source copy; no harness source enters product directories in the task worktree.

- [x] Create explicit startup/help/process/output/catalog test boundaries in copy and validate data isolation.
- [x] Build and inspect harness boundaries; exercise real controls/window ownership through native UI.
- [x] Record observed behavior/failures and scope limits; reproduce any production regression before fixing.


### Task 13: Graphic designer command codec and quoted-value defect

**Files:** Helper/CreateConfigHelper/GraphicDesignerCommandCodec.cs; existing GraphicDesignerHelper facade; Views/CreateConfigHelper/GraphicDesignerSettingItemModel.cs; Presentation/GraphicDesignerCommandCodecTests.cs and source link.

- [x] Read and characterize current parse/build semantics and source call sites; write absent-API characterization tests.
- [x] Extract pure model/command conversion unchanged, retain original facade and configuration factories.
- [x] Reproduce quoted unknown-flag roundtrip loss with a real failing test, then fix quote handling; investigate values containing equals before deciding any additional change.
- [x] Full tests/GUI/independent review; repeat relevant live roundtrip and next joint check with the latest completed security commit.

Code checkpoint 27bfa46: 985/985 tests and GUI x64 Release with zero warnings/errors; independent follow-up review found no remaining actionable defects. Native post-fix editor/designer roundtrip retained the closing quote and showed zero errors/warnings; real enum selection and proxy scalar reads also passed. Joint compatibility with security d65477f passed 1069/1069 tests and GUI with zero warnings/errors. Values containing equals were already preserved by the original first-separator split and required no additional fix. Security d65477f still has its separately reported open filesystem gap; this joint result establishes compatibility only.

### Task 14: BlockCheck2 result and report ownership

**Files:** create ViewModels/BlockCheck2RunResultViewModel.cs, IBlockCheck2ReportOperations.cs and BlockCheck2ReportOperations.cs; modify existing BlockCheck2ViewModel.cs/.SiteLists.cs and additive test source links; create Presentation/BlockCheck2ResultTests.cs. The existing public BlockCheck2ResultViewModel remains the window/manual-assignment model.
**Interfaces:** owner constructor (IBlockCheck2ReportOperations, BlockCheck2OptionsViewModel); SetPreparedIssues(IReadOnlyList<BlockCheckIssue>) is quiet; ApplyRunResult(BlockCheckSessionPreparationResult,BlockCheckRunResult), SetOperationError(string), SetHistorySaveError(string), Reset(Action<Action> resetProgress); existing result/report properties and three Save*Async methods. Report operations exposes Build(catalog,preset,targets,result), SerializeJson/Text(report), SaveJson/Text/PresetArgumentsAsync(path,report,token). Actual adapter retains existing service instances.

- [x] Write characterization tests for empty/failed/best-effort/HTTP-only reports, exact issue matching, session identity/live option timing, partial failure state, lazy serialization and repeated reads, synchronous missing-report export, path/token/task forwarding, exception propagation, repeated error notifications and reset order with the real progress owner.
- [x] Run tests against absent API and retain RED evidence. Extract state/algorithms; preserve host event forwarding and async history/CTS ordering.
- [x] Run targeted and full tests, GUI x64 Release and diff checks. Independently review against aeeba14; reproduce actionable findings before fixing.
- [x] Verify actual host PropertyChanged sender/order and completion notices in an isolated fixture-backed WinUI run; do not launch a real scan or export dialog.
- [x] Record aggregate class reduction, limitations and verified local checkpoint; leave the overall timed goal active.

Code checkpoint 7188106: 35 targeted cases (including two reentrant scenarios), full 1020/1020 and GUI zero warnings/errors. Review-found name collision and a residual field reference in SiteLists were confirmed by GUI compiler errors and fixed; the pre-existing public model matches the baseline blob exactly. Native isolated host verification passed all 11 result events and all 29 reset events, correct sender/current snapshot/serialization/export flags, and both real completion InfoBars. Fixture process exited zero; real scans/history/disk exports/public Reset were not invoked. Host aggregate 675 to 607 physical lines.

### Task 15: Manual assignment editor of the BlockCheck2 result window

**Files:** create ViewModels/BlockCheck2ManualAssignmentEditor.cs and BlockCheck2ManualIssueItem.cs; modify existing BlockCheck2ResultViewModel.cs and BlockCheck2ManualResultViewModels.cs; source-link existing Draft/manual models/rules plus new editor into test project; create Presentation/BlockCheck2ManualAssignmentEditorTests.cs. Preserve the original nullable annotation context for linked legacy sources.
**Interfaces:** editor constructor (BlockCheck2PresetDraft draft, Func<BlockCheckResultSession?> session, Func<IReadOnlyList<BlockCheck2StrategyEvidenceItem>> evidence, Action clearAcknowledgedIssues, Action<bool> rebuildPreset, Func<StrategyDefinition,string>? formatActions=null). Items aliases Draft.Assignments. Methods AddForSite/List, RemoveAssignment, MoveAssignment, ClearManualAssignments, GetSiteListPaths, CanAddForSiteList, SeedAutomaticAssignments(session), ClearAutomaticKeys, RefreshEvidenceSelections, RefreshAssignmentIndexes retain host signatures/behavior. Host methods delegate; rebuilding/issue localization/manual tests stay in host.

- [x] Write characterization tests for collection identity, guard/duplicate/missing definitions, scope compatibility/ignored targets/path sorting, move/remove/clear callbacks, automatic seed primary/fallback/scope/de-duplication, selected/automatic flags, Draft timing, exception/partial state and reentrant collection notifications.
- [x] Observe absent-API RED, move localized issue model intact and extract assignment ownership without altering rules or service calls.
- [x] Run targeted/full tests, GUI and source/API comparison; independent read-only review vs 59a5e8f, reproduce and fix actionable defects.
- [x] Verify relevant real model/collection/UI behavior in a pinned isolated fixture; record evidence, scope limits and class reduction before finalizing the task.

Code verification: 34 targeted cases, full 1054/1054, GUI zero warnings/errors; independent review and follow-up found no introduced defects. All 13 extracted bodies compare equal after dependency/name substitutions. Public result window model 565 to 402 physical lines; new editor 229. Native Add/Move remains pending. Existing comparer/provenance/scope/reentrancy semantics are characterized and preserved, without claiming their separate baseline risks fixed.

Native verification investigation: the initial direct-TabView fixture omitted the production Frame/MenuBarHost contract. Three fixture runs did not reach an Add/Move observation; run2 Windows error c00000fd and run3 actual managed UI-thread stack identify AnimatedSelectorBarMenu.ScheduleRetry with a permanently null MenuBarSession. The corrected fixture navigates through a real Frame with MenuBarHost attached, asserts a non-null session before mounting, builds with zero warnings/errors, and passed independent read-only review. All tracked GUI/Shared/Core/AddOns files still differ only at the ten explicit external boundaries. Native repetition is pending while a separate ToolsSmokeTest.exe UAC occupies the common desktop; the exact requested path is unverified and no elevation consent was granted. Keep the last checkbox open until actual Add/Move observations pass.

### Task 16: ConfigMaker document workflow coordinator

**Files:** create Helper/CreateConfigHelper/ConfigMakerDocumentCoordinator.cs, IConfigMakerDocumentHost.cs, IConfigMakerDocumentStorage.cs and Tests/Presentation/ConfigMakerDocumentCoordinatorTests.cs. Modify host ConfigMakerViewModel.cs/.Document.cs, existing ConfigMakerPresetStorageService interface declaration only, PresetDocumentConverter's inherited static qualification, and additive test source links for coordinator/interfaces/actual document+converter/save-result/request.
**Interfaces:** coordinator constructor (ConfigMakerPresetDocument document, IConfigMakerDocumentHost host, Func<IConfigMakerDocumentStorage> createStorage, Func<ConfigMakerPresetDocument,string,Task> export). Methods retain host names/signatures for LoadPresetDocument, LoadConfigItem, CreateConfigItem, SaveToApplicationAsync, NewDocumentAsync, SaveTextAsync, SyncDocumentFromEditor. Storage SaveAsync(string,ConfigMakerPresetDocument)/OverwriteAsync(ConfigMakerPresetDocument) return Task<ConfigMakerPresetSaveResult>. Host supplies ComponentId get/set, EditorText, IsEditorReadOnly, SetEditorText, SetBaseDirectory, RebuildPresetStructure, UpdateSimpleDesignerAvailability, NotifyDocumentStateChanged, AttachExistingPresetFiles, ApplyAutoCorrectorSilentlyAsync(ConfigItem), GetConfigItemBaseDirectory(ConfigItem), AskSavePresetAsync(name,overwriteEnabled), ConfirmNewDocumentAsync(), ChooseExportPath(bool), ClearPresetStructure, ClearEditorUndoHistory, Text(key), ShowDocumentMessage(message,isError). Explicit host implementations forward to existing UI/helpers without changing public API.

- [x] Write characterization tests using actual document/variable/resource/converter models for load clone/identity/self-load/event timing, config import callback/await order, create current text, sync-attach-sync, dialog cancellation/overwrite, storage factory timing, result identity/live metadata, first alias handling, ordinal cancellation, exceptions/partial state, overlapping saves, New readonly/content/confirmation/clear order and retained metadata, export format permission/cancellation/catch boundaries. Add exact legacy conditional conversion coverage.
- [x] Run absent-owner API RED; preserve it. Implement coordinator by moving the existing workflow, retain private filesystem/autocorrect methods in host. Replace converter's inherited static qualifier with the declaring LScriptCore and link actual models; no test-only model stubs.
- [x] Run targeted/full tests and GUI x64 Release, compare public API/source scopes and run diff checks. Independent read-only review against a260690; reproduce/fix actionable regressions before checkpoint.
- [x] Record class aggregate and verification limits, create scoped local code checkpoint.
- [x] Add fixture-backed native workflow observations when the common desktop is available; fake storage/export in the copy, never invoke actual saves or file deletion for this verification.

Code verification: 46 new cases, full 1100/1100 and GUI zero warnings/errors; independent read-only review found no introduced defects after comparing all eight moved bodies and explicit host adapters. Document partial 242 to 113 physical lines, aggregate ConfigMaker host 1525 to 1400, coordinator 197. GUI inputs unchanged after its successful build; final tests include seven additional characterization cases. Native host integration remains pending; actual storage/export/autocorrect effects are unchanged and were not executed.

### Task 17: Actual ConfigMaker document host integration on the UI thread

**Artifacts:** ignored DocumentHostSmokeBootstrap.cs, prepare-document-host.py, check-document-host-boundaries.py and build-document-host.ps1; new pinned source copy C:/Users/lumen/Programming/cdpiui-host-f7e5454-20261109. No harness enters product source tree.
**Scope:** instantiate the actual ConfigMakerViewModel on Application's DispatcherQueue thread with an inert IConfigMakerView. Keep the complete host, coordinator, document, converter, structure adapter and XAML assets unchanged. Explicit fixture boundaries: Program/Main and App startup; own-bin-only DataDirectory; resource facade catalog path lookups; ComponentCommandHelpService.LoadAsync; storage SaveAsync/OverwriteAsync and exporter ExportAsync entrypoints. No actual storage, exporter, auto-correct, component test, scan, history, shell, window navigation or desktop input.

- [x] Create fresh pinned copy, preserve all older copies, and verify every tracked GUI/Shared/Core/AddOns source/asset against f7e5454; require exactly seven changed boundary files plus one explicit bootstrap.
- [x] Independently review boundaries and assertions before execution; build with zero warnings/errors. The executable manifest remains asInvoker; fixture asserts portable own binary data root and UI-thread affinity.
- [x] Exercise real host Load, help, deferred Save, exporter success/failure/cancel, readonly/cancel/deferred-confirm New, ConfigItem creation/load. Assert document/collection identities, command/editor/diagnostics state, actual event sender and partial-state order, resource updates and status severity/message. Export/storage are inert and no physical config is saved.
- [x] Confirm process exit zero and exact PASS groups/no FAIL; record logs/pin/hash and scope limits. This closes real-host adapter runtime coverage only. Keep interactive Task15 Add/Move and Task16 control UI gates open until real observations pass.

Verified actual-host integration: exact product f7e54545ba27fee6622e79bc2f95f65c3df88aad; 1190 tracked GUI/Shared/Core/AddOns files, seven explicit boundaries plus bootstrap/verified empty Secret stub. All seven assertion groups passed; PID27288 exited zero, no FAIL, no stdout/stderr. Full literal document state sequences8/3/4 and independent current-editor read were strengthened after review before execution. Build zero warnings/errors; main product inputs unchanged. DLL SHA25627607C62EFF27363E30F491D5D5C5FE5B8B6D11CEE103F77B1639DDC631714F9. Interactive control gates remain open.

### Task 18: Missing preset file replacement workflow

**Files:** create Helper/CreateConfigHelper/ConfigMakerPresetFileReplacer.cs, IConfigMakerPresetFileReplacementHost.cs, IConfigMakerPresetFileReplacementBackend.cs (including auto-corrector contract), ConfigMakerPresetFileReplacementBackend.cs and Tests/Presentation/ConfigMakerPresetFileReplacerTests.cs. Modify host main/PresetFiles partials and additive test source links for owner/contracts/existing choice enum. Keep Document partial's silent wrappers compatible; do not change actual resource/autocorrect implementations.
**Interfaces:** replacer constructor(host,backend); ReplacePresetFileAsync(ConfigMakerPresetFileInfo?), TryCreateEmptyPresetFile(string,bool=true), TryApplyPresetFileReplacement(ConfigMakerPresetFileInfo,string,bool). Host exposes RunReportingErrorsAsync(Func<Task>), AskPresetFileReplacementAsync(path), ChooseReplacementFile(path), Text(key), ShowPresetFileMessage(message,severity), RebuildPresetStructure(), UsesExplicitPresetStructure, ConfigMakerPresetFileInfo[] ExplicitPresetFiles get/set, SetCommandText(string), NotifyPresetFileReplaced(originalPath,replacementPath). Backend exposes TryResolvePath, FileExists, CreateEmptyFile, TryUpdateAttachedResource, ReplaceReferences and CreateAutoCorrector; corrector ShouldSuggestEmptyFile(string)/Task<string?> FindReplacementAsync(string). Backend constructor only stores the existing facade. Notice severity is Success/Warning/Error and maps explicitly to WinUI in host.

- [x] Write tests for null/invalid/existing/cancel/manual/auto paths and asynchronous timing, lazy corrector factory, empty-file/suggestion/manual fallback rules, attached-vs-reference branch, normalized path checks, explicit array identity/matching/retained fields, conditional formatted command assignment, post-rebuild event timing, silent status, partial mutations and all catch/exception propagation boundaries. Use no real filesystem/shell operations.
- [x] Observe absent-owner API RED and preserve output. Extract workflow and add thin production adapters, retaining public signatures, silent Document callers, conditional event construction/sender and actual algorithms.
- [x] Run targeted/full tests, GUI x64 Release, source/diff checks; independent read-only comparison with8039f8a. Reproduce and fix introduced defects before checkpoint.
- [x] Record aggregate reduction, limitations and scoped local code checkpoint.
- [x] Add actual-host fixture verification without filesystem effects if possible. Keep prior interactive gates separate.

Code verification: 46 new targeted cases, full1146/1146 and GUI zero warnings/errors. Read-only independent review found no introduced defects in all seven moved methods, host/backend mappings and silent callers. File partial327 to178 physical lines, aggregate host1400 to1254; replacer194. No actual filesystem/shell operations executed in the new tests. Actual-host event/backend runtime check remains separate; conditional event sender/read and synchronous snapshot-before-Task.Run are statically verified so far.

### Task 19: Actual missing-file replacement host integration

**Artifacts:** new ignored ReplacementHostSmokeBootstrap.cs, ReplacementHostCases.cs and make/prepare/check/build-replacement-host scripts; fresh pinned copy C:/Users/lumen/Programming/cdpiui-replace-01163f0-20261109. Preserve all older copies. Product pin01163f080d96139111d06970ad384825cbe467f6.
**Scope:** actual host/replacer/document/registry/reference editor and Task.Run wrapper. Eight boundary files: same seven startup/data/catalog/help/storage/export boundaries as Task17, resource snapshot creation inert, plus low-level replacement backend FileExists/CreateEmpty/FindReplacement target. Do not call original autocorrect search, physical file creation, public silent-autocorrect import, component testing, shell, history or desktop input. Test silent private helper forwards directly on the UI thread. Keep document regression groups at the new constructor/host composition pin; do not count them as new unit tests.

- [x] Create and source-check the fresh copy: all tracked GUI/Shared/Core/AddOns sources/assets compared to pin, exactly eight boundary files and only two bootstraps/empty Secret stub added. Actual host/replacer/registry/reference editor remain unchanged.
- [x] Build zero warnings/errors and independently review safety/assertions before execution. Require own binary data root/asInvoker and thread affinity; bounded worker wait always released in finally.
- [x] Verify manual reference replacement with resource-event reentry and post-rebuild command change, explicit array and variable updates, real event sender/args/status; attached branch preserves command/array/event absence; auto snapshot is prepared on UI thread before worker and uses old component while live editor/component change; empty branch and direct silent helpers have expected event/status behavior with no physical files.
- [x] Run all twelve assertion groups (seven document composition, five replacement groups), require process exit zero/no FAIL, record logs/hash/pin and exact scope. Keep interactive control gates distinct.

Verified actual replacement host integration: PID43396, exact01163f080d96139111d06970ad384825cbe467f6; twelve groups passed, exit0, noFAIL/stdout/stderr. Build0warnings/errors; independent1431-file archive comparison,1194 within the four product roots; eight boundaries/two bootstraps/empty stub. Manual/attached/auto/empty/silent helper paths covered with actual host/registry/reference editor, UI snapshot and worker/return affinity. Full silent-autocorrect import, physical IO/search and interactive controls not executed. DLL SHA25610A8E8BD2B5238461F8D6DB9FD44C5CFF361F6AA06E40101EA744E7C975B04EE. Root1146/1146 unchanged.

### Task 20: Remaining preset file tree actions

**Files:** create ConfigMakerPresetFileActions.cs, IConfigMakerPresetFileActionsHost.cs, IConfigMakerPresetFileActionsBackend.cs, ConfigMakerPresetFileActionsBackend.cs under Helper/CreateConfigHelper and Presentation/ConfigMakerPresetFileActionsTests.cs. Modify host main/PresetFiles and additive test source links. Reuse existing file-notice enum and real document/resource/file models; no new package.
**Interfaces:** constructor(IList<ConfigMakerPresetResource>,host,backend), AddPresetFiles(IEnumerable<string>), AddPresetFilesFromPicker(), OpenPresetFileAsync(FileInfo?), ShowPresetFileInFolder(FileInfo?), RemovePresetFileAsync(FileInfo?), InsertPresetFileReference(FileInfo?). Host: RunReportingErrorsAsync, ChoosePresetFilesToAdd, AttachExistingPresetFiles, RebuildPresetStructure, SetPresetFilesPanelVisible, Text/ShowPresetFileMessage, AskTextFileApplicationAsync, ConfirmRemoveReferencedFileAsync, ScheduleDiagnostics, IsEditorReadOnly, InsertAtCursor. Backend: AddFiles, ResolvePath, TryResolvePath, FileExists, GetOrCreate(fullPath,kind,out bool), FindAttached, IsReferenced, ShouldAskForTextApplication, RememberTextFileDialogShown, OpenTextFile, OpenFileInDefaultApp, ShowFileInFolder.

- [x] Characterize original enumerable/count/picker/add order, text/nontext open and dialog await/preferences/shell routing, missing/show sync exceptions, null/unattached/referenced removal, resource identity across confirmation/reentrant subscriber failures, readonly/preset URI/direct path insert, full-path timing and late resource alias after rebuild. No real filesystem/shell/preferences execution.
- [x] Observe absent-owner API RED, then move unchanged workflow into owner and add thin production adapters. Preserve private picker method and command identity, original catch scopes and all public signatures.
- [x] Run targeted/full tests, GUI x64 Release and diff/source checks; independent read-only comparison with29b05a8, reproduce/fix introduced defects before checkpoint.
- [x] Record aggregate/class reduction and scope limits; create scoped local code checkpoint.
- [x] Add actual-host fixture coverage when feasible while preserving existing pinned copies and interactive gates.

Code gate: 47 targeted cases, full1193/1193 with no failed/skipped; GUI x64 Release0warnings/errors47.43s. Independent read-only review accepted all seven transferred methods, twelve backend forwards and host composition; no findings or declined-to-judge items. PresetFiles178 to79 physical lines, aggregate1254 to1158, new owner152. Real settings/shell/filesystem and interactive controls not executed by the unit suite.

### Task 21: Actual file-actions host integration

**Artifacts:** fresh pinned copy C:/Users/lumen/Programming/cdpiui-actions-a87f27b-20261109; FileActionsHostSmokeBootstrap.cs, FileActionsHostCases.cs and make/prepare/check/build scripts in ignored plan scratch. Product pina87f27ba078f1d8041f005436f356ec302ae65c7. Keep all older copies intact.
**Scope:** actual ConfigMaker host, file actions/replacer/document owners and registry/reference editor. Same eight startup/data/catalog/help/storage/export/replacement boundaries asTask19, plus actions backend preference and shell calls recorded instead of executed. Real action File.Exists/path/registry operations retained. After exact own-bin/asInvoker/data-root guards create exactly three harmless new input files using FileMode.CreateNew, preserve their bytes and leave them in the fixture. Never overwrite/delete them or execute shell/settings, original autocorrect search, real storage/export/component tests or desktop input.

- [x] Source-check pinned four product roots, exactly nine boundary files and three bootstraps/empty Secret stub. Build zero warnings/errors; independent safety/assertion review before execution.
- [x] Verify actual add/picker filters, kinds/metadata/tree/counts/command identity; text open dialog await true/false and inert shell routes with real file resolution; missing/null warning guards; remove original object after pending localized confirmation and actual rebuild/timer; insert real registry with alias mutation during rebuild and real editor/cursor/event order.
- [x] Run all seventeen assertion groups, require exit0/noFAIL and unchanged input bytes; record hash/log/pin and exact runtime limits. Keep interactive gates15/16 open.

Runtime gate: PID37800, exit0, all17PASS/COMPLETE, noFAIL/stdout/stderr. Final reviewed fixture build0warnings/errors41.13s; independent all1436tracked-file comparison including1198 in product roots, nine boundaries and three bootstraps/empty stub. Before execution fixed byte-comparison, timer-precondition and nonnull-readonly assertion gaps; product unchanged. DLL80A6485ADEFEDE1D24FDEBBAF2A39538DBC7D82222D90AEEA7FC19013AC1396B. Three CreateNew input files retained with exact bytes; real shell/preferences/storage/export/search/components and interactive controls not executed. Full attach ordering/exceptions characterized by Task20 unit tests and static review rather than exhaustively by this runtime suite.

### Task 22: Caller-visible source API inventory

**Artifacts:** ignored api-compatibility project/input/controls/logs in plan scratch. Uses Roslyn bundled with the existing SDK8.0.427; no product dependency or source changes. Pins f39e8a0 to00fb0ad (producta87f27b).

- [x] Read all tracked GUI C# source blobs from both pins, parse without executing product initializers, compare existing exported type declarations and explicitly declared public/protected method/property/accessor/event/field/constructor signatures, generic constraints, parameter defaults and enum member values.
- [x] Treat async implementation modifier and inaccessible private/internal accessors as implementation details. Validate a positive control for async/private-setter changes and a negative control that independently removes a public setter, changes a property type and changes a parameter default.
- [x] Record exact syntax-only scope and evidence; keep runtime/binary/API-synthesis claims separate.

Result:451original exported types across517baseline/559final GUI C# files, zero removed/changed compared signatures. Positive control exit0; negative control exit2 with exactly3expected changes. No product change, full suite1193/1193 and GUI0/0 remain tied to the unchanged product checkpoint. This inventory does not resolve aliases/base/interface lists, inherited/synthesized members, attributes or binary ABI, and does not replace runtime/source review.

### Task 23: Whole-branch review and one fix pass

**Range:** f39e8a0 to00fb0ad; fresh whole_branch_review using review package/ledger/spec, all95files. No Critical, one Important incomplete quoted codec repair, one Minor duplicate mapping. Root accepts Important by actual effect despite inherited baseline defect; it belongs to the already authorizedTask13 quoted-value repair. No second reviewer after the fix.

- [x] Regrade findings and record all declined scopes/rulings; defer Minor duplicate facade/editor file-kind mapping.
- [x] Reproduce repeated quoted root-relative/dash-leading path loss and literal known-option filename activation with RED tests. Keep true whole-quoted options after bare flags and unquoted separate legacy boundaries.
- [x] Preserve explicit inline ownership for unknown values resembling options; distinguish whitespace before/after first equals in whole quoted tokens. Validate three repeated visits, known/unknown and inline/separate paths, whole quoted/bare inline forms.
- [x] Run all codec tests67/67 and full suite1214/1214 nofailed/skipped; GUI x64Release0warnings/errors11.67s. Scope checkpoint; no arbitrary minor fixes or second review.

RED evidence: final-codec-red.log11fail/4pass; extra same-root-cause inline control final-codec-inline-ownership-red.log6fail/3pass. Final GREEN final-codec-complete-green.log67/67; final-codec-fix-tests/gui logs.21new cases. Update exact-pin facade runtime and joint compatibility separately; older17groups and1328joint results retain earlier pins.

### Task 24: Final codec facade integration and refreshed compatibility

**Artifacts:** new pinned C:/Users/lumen/Programming/cdpiui-facade-80521fc-20261109, CodecFacadeCases.cs plus cloned own-root guarded bootstraps/scripts. Product80521fcfd62ac393c05def42cfe7322a3a728250. Prior fixtures unchanged. No new external boundary beyond the same nine accepted inTask21; only one additional pure in-process codec test file and Run call. No added windows/desktop input or component/settings/shell/filesystem operation. Three prior CreateNew inputs remain part of the reused seventeen host groups.

- [x] Source-check all tracked four-root files against pin: exactly prior nine boundary files, four bootstrap files/emptySecret. Inspect new codec call paths and generated root/pin/build/run guards before execution; build0warnings/errors.
- [x] Verify actual GraphicDesignerHelper catalog/facade/models and dynamic factory on DispatcherQueue: three repeated path/ownership visits, genuine whole quoted options, decoded proxy scalars. Retain prior seventeen host assertion groups at the final constructor/codec pin; require21PASS, COMPLETE, exit0/noFAIL/stdout/stderr.
- [x] Refresh joint compatibility of80521fc with documented committed security38ec905 (owner911/911). Never include security cleanup WIP; preserve older snapshots. Record exact pins/hash/logs and unverified native control limits.

Facade fixture sourcechecker1198tracked productroot files, nine boundaries/four bootstraps/emptySecret. Build0warnings/errors82.87s. PID39492, exit0,21PASS+COMPLETE/noFAIL/stdout/stderr; three inputs byte-identical and retained. DLL9273C5C3D990566EFDFB7F622007E234191FCDAC4826637C0E537D5C7E2363C6. Four new groups use actual facade/catalog/models/dynamicfactory, no controls or external operations. Root inspected added pure call paths and unchanged accepted boundaries; no second product review. Joint80521fc+38ec905 suite1357/1357 nofailed/skipped49s; GUI0warnings/errors81.08s. Security cleanup WIP excluded.

### Task 25: App file activation routing

**Base:**906b218d807ebfa73c79d583f1b0e970ceee85dc; previous whole-branch review/fix remains complete. This new App extraction gets its own review of the new range, not re-review of the fixed codec. **Files:** new Helper/Basic/AppFileActivationHandler.cs, IAppFileActivationOperations.cs, AppFileActivationOperations.cs, Presentation/AppFileActivationHandlerTests.cs; App main/Activation and additive test links only. All four new paths verified absent.
**Interfaces:** handler(operations).ProcessFiles(string[])->Task<bool>. Operations: LogFile(string), PathExists(string), IsConfigShareSupported(string), OpenConfigShareAsync(string), OpenConditionalTaskAsync(string), OpenUpdateFileAsync(string), OpenStorePackAsync(string). Production constructor(App,Func<string,Task> conditional callback), store-only. Source algorithms/importers/security/Program/migration/dispatcher contracts unchanged.

- [x] Write characterization and observe absent-owner API RED: guards, whitelist/case/all-quotes/no-trim, order/precedence/first-file, faults/cancellation, await all route tasks and reentrant live array.
- [x] Transfer ProcessFiles and static whitelist; keep private App forward and store-only composition. Move exact share/patch/store window actions into thin production port; conditional method unchanged and invoked through original callback. Preserve fire-and-forget pack import and debug log source.
- [x] Targeted/full tests and GUI x64Release, independent read-only review of all new range/mappings vs906b218, resolve introduced defects with RED/GREEN before checkpoint. No real files/windows/import/update actions.
- [x] Record reduction, exact scope and local checkpoint. Consider source-boundary runtime integration if useful; do not duplicate unchanged older gates or claim full startup/import execution.

Verification: absent-ownerCS0246 RED;41targeted PASS, initialfull1255/1255+GUI0warnings/errors45.93s. Added4real AppActivationDispatcher+handler composition cases (initial file/argv suppression, startup error catch, redirected error propagation, redirected launch fallback); finalfull1259/1259 nofailed/skipped. After GUI only tests/docs changed. Independent review accepted routing and all7production port methods/composition/endpoints, no findings; real startup/windows/import/security execution excluded. App aggregate794->754, activationpartial259->215, owner55/backend50. Additional async adapter task frames noted but effect/order/context/fault/cancel semantics retained by source comparison. Optional actual-App private-forward/adapter-order fixture considered separately, with inert fake window/import targets only; no runtime import claim.

### Task 26: Actual App file routing composition with inert window targets

**Decision by executor under delegated autonomy:** close the new production adapter order/composition gap using a fresh pinned copy, not real imports or windows. Product c16a18afe72255d9ef19c8b6d9fc815fde492100. Derive from Task24 facade harness into a NEW short path; never rewrite an older copy. Keep prior21groups. Added AppFileActivationCases.cs is scratch/bootstrap only. Exactly one new boundary file (AppFileActivationOperations.cs), total10; five bootstraps/emptySecret. App ctor before InitializeComponent, handler and activation dispatcher remain actual source; preserve the prior safe startup cut after it.

**Boundary details:** production operations LogFile/PathExists targets record/consult own-bin fake markers. IsConfigShareSupported remains actual safe extension predicate. Replace only share/update/store factory targets with bounded plain-object fake window factories, and ActivateWindow targets with recorder; keep original await/create/set/activate and fire-and-forget method bodies. Fake object SetFileAsync waits a controlled task, SetUpdateFilePath records synchronously, SetPackFilePath returns an intentionally incomplete task. Replace conditional invocation target with inert task; reflection independently asserts the stored original callback target/method and App instance identity before tests. Do not call real callback. All marker paths exact direct children of guarded bin. No actual WinUI window instance, real file/package load, update, settings, shell, pipe, tray, driver or component operation is allowed.

- [x] Prepare five new runtime assertion groups: actual App/handler/operations/dispatcher field identities and empty/unsupported/missing guards; share awaits factory then activates before awaited SetFile; patch set-before-activate and store awaits factory with default activate=true but does not await pack import; conditional captured callback identity plus awaited inert target; actual private forward/redirected File dispatcher preserve result/fault/cancel and first-file behavior. Thread affinity for every recorded operation. Retain26total groups.
- [x] Make all pending factory/import tasks bounded, release/cancel them in finally and observe completion even on early assertion failure. Source checker all four-root files against pin, exactly10boundary files and five bootstraps/emptySecret; additionally inspect all new call paths and constructor retention. Build0warnings/errors, independent safety/assertion review before execution.
- [x] Run asInvoker own-bin/no-window fixture only after review; require26PASS, COMPLETE, exit0/noFAIL/stdout/stderr. Preserve three prior CreateNew input files and byte checks. Record exact scope/pin/hash/logs, source comparison and process exit; do not count fake windows as native UI or full production startup/import. Keep prior interactive15/16 gates open. No need to repeat unrelated source checks until a new concern justifies them.

Runtime targets are deliberately substituted; success would prove App private forward and the retained production adapter orchestration with inert targets, not actual WinUI window factory/activation or importer correctness. Do not claim actual Path.Exists execution in this fixture (unit/static comparison already establishes original mapping); markers replace it. Joint compatibility can be refreshed at a later stable code freeze rather than for every docs/harness step.

Verified: independent safety/assertion review accepted1201tracked productroot files, exactly10boundaries/fivebootstraps/emptySecret; App ctor before InitializeComponent exactpin. Build0warnings/errors65.73s. PID44836 started07:55:20.1981878Z, exit0at07:55:22.3576511Z;26PASS/COMPLETE/noFAIL/stdout/stderr. DLL57584342D4D1ED45EF73F3B9937565053D0630E80DDD28F87C5B79E82055FECC. No mandatory fixture fixes. ActualApp bindings/handler/adapter sequence/dispatcher proven with inert targets; actualwindows/imports/Path.Exists/startup unexecuted. Three input files retained exactbytes; postrun sourcecheckPASS.

### Task 27: BlockCheck2 manual preset state

**Base:**2d4202dd6f4c406c7484ead6612703f758b318c2. **Files:** create ViewModels/BlockCheck2ManualPresetState.cs, IBlockCheck2ManualPresetHost.cs, IBlockCheck2ManualPresetOperations.cs, BlockCheck2ManualPresetOperations.cs and Presentation/BlockCheck2ManualPresetStateTests.cs; modify public BlockCheck2ResultViewModel and additive test links only. All5new paths verified absent; never overwrite public existing result class. Owner(Draft,IReadOnlyList<ProbeResult>,host,operations), Rebuild(bool), ClearResult(), Arguments, static OptionsFor(session). Host Session getter/ClearIssues/AddIssueGroup(severity,code,message,count,IEnumerable<string>subjects)/RefreshAssignmentIndexes/RefreshEvidenceSelections/NotifyManualState. Build port takes catalog, targets, assignments, evidence, options and returns existing result.

- [x] Write behavior tests with real Draft/assignment/domain types and fake host/Build; absent-owner API RED. Cover empty/populated ordering, update flag/expert state, deferred overlay/comparers/targets/current session, options/grouping/subjects, live callbacks and partial failures.
- [x] Move result/rebuild/subject/options logic, preserving private host forward, exact Load clear position, localized issue/ack adapter and same domain service. No direct network/testing/assignment editing policy changes; no UI localizer stubs.
- [x] Run targeted/full tests and GUI x64Release; independent new-range read-only review vs2d4202d including real host/IssueItem/service callbacks. Resolve introduced defects with RED/GREEN before local checkpoint.
- [x] Record physical reduction/exact scope, tests/review/risks and checkpoint. Assess separate real public-model runtime using initialized localizer and pure manual build; never call live TestStrategyAsync.

Verified: absent-ownerCS0246RED, initial36targeted/full1295+GUI0warnings/errors49.24s. Independent review accepted all transferred logic, host/state/service bindings/localizedIssueItem/ack mapping with no findings. Added3cases for populated Profiles reaching real Draft.Groups and invalid site-list scope failure/blank laziness, final39targeted and full1298/1298 nofailed/skipped. After GUI only tests/docs changed. Public result402->319physical lines, state133/backend18; no public API/collection/sender/network/domain builder change. Real public model/localizer/ack filtering/production Build runtime remains separate; do not count fake host tests as it.

### Task 28: Actual manual preset public-model integration

**Executor decision under delegated autonomy:** verify actual public model/localizer/ack/domain-Build composition with the initialized Application DispatcherQueue and pure fixture data. Product a29b1f19fe9f22167290106e039e74830c3a7028. Derive NEW C:/Users/lumen/Programming/cdpiui-manualstate-a29b1f1-20261109 from Task26 App harness. Keep prior26groups, add ManualPresetHostCases.cs as sixth bootstrap and five groups (31total). Same10boundary files: no changes to public model/state/editor/IssueItem/preset service/writer. Never run live TestStrategyAsync, probes/components, persistence/history, windows or UI controls. Inspect ctor/call paths before execution.

**Fixture data:** reuse only the pure two-strategy/one-target/catalog/report pattern from ignored ManualSmokeBootstrap.cs around290-310, never its window/page setup. fixture-split(multisplit,pos1), fixture-disorder(multidisorder,pos2), three successful attempts each, TLS12/IPv4 example.test. Actual ReportBuilder/preset builder. For site-list scope use existing own-bin actions-sites.txt from prior groups and explicit Target.HostListPaths; no new file/user path.

- [x] Five groups: Load/session/report/evidence and stable identities; programmatic Add(split),Add(disorder),Move(split,1),Clear with independent action arrays and12hostnotifications per edit; overlapping site+site-list scopes yield localized MANUAL_SCOPE_OVERLAP/subjects, QuickFix acknowledges exact key and private Rebuild(false) stays suppressed until editing clears ack; Load(report) observes previous result during ManualIssues.Clear then clears at original point, same collections; an already-completed manual failure ProbeResult injected only into existing private list affects actual builder overlay (no live test), and reload clears that same list. Check nonempty Draft.Groups.
- [x] Literal actions from previous fixture: split --lua-desync=multisplit:pos=1; two same-site assignments --lua-desync=circular:fails=3:maxseq=32768:inseq=4096, --lua-desync=multisplit:pos=1:strategy=1, --lua-desync=multidisorder:pos=2:strategy=2:final; moving swaps action order/indices. Inspect unchanged builder for precise failure issue expectations, never compute expected output by re-running the implementation under test.
- [x] Fresh paths guard; sourcechecker all4roots exactpin, same10boundaries/sixbootstraps/emptySecret. Inspect new pure ctor/Build/localizer/assignment call paths; build0warnings/errors; independent safety/assertion review before first execution.
- [x] Require31PASS/COMPLETE/exit0/noFAIL/stdout/stderr, process finished, prior3inputs byte-identical retained. Record pin/hash/scope; no native-control/live-test claim. Keep gates15/16 open and corrected native921 fixture preserved/unlaunched.

After this freeze refresh joint compatibility at a documented committed security pin (latest read a99d0d0, owner926/926). Do not include promotion WIP/edit other worktree. Preserve all previous copies/logs; no routine unchanged root-test reruns for harness/docs.

Verified:1205tracked productroot files, same10boundaries/sixbootstraps/emptySecret; independent safety/assertion reviewACCEPT, no mandatory fixes. Build0warnings/errors69.67s. PID35764 start08:26:29.9213722Z, exit0at08:26:32.3312406Z:31PASS/COMPLETE/noFAIL/stdout/stderr. DLLD320B404D6DCCB1C85440F314AE88B14D613DDD2BCDE99D30EAD04D09CD1E1A6. Manual models/state/editor/IssueItem/localizer/service/writer unchanged froma29; actualactions/events/ack/reset/overlay passed, no live testing/controls. Beforeexecution root refined assertion to move-back originalkey and canonical domain strategyid; no productchange. Threepriorinputs exactbytes retained, processgone.
Joint a29b1f19fe9f22167290106e039e74830c3a7028+a99d0d0c0c433f9878fdfc0b4af7fa4a79bb4c73:1456/1456 nofailed/skipped74s, GUI0warnings/errors62.24s. Explicit productpin allowed docs-only HEAD via source-diff guard, clean working tree at snapshot, no product overlap, additive testitems only. Promotion WIP excluded; old snapshots preserved.

### Task 29: Manual strategy test controller

Base83a44cf, producta29b1f1. Executor accepts the above bounded extraction under delegated autonomy. Keep service/list initialization, public methods and actual domain behavior. No live TestWithCdpiuiAdaptersAsync execution in checks.

- [x] Write actual EvidenceItem/Draft tests with fake operations; absent-controller API RED. Cover no-session and sync argument failures, fresh options/identity/timeouts/token, pending/fault/cancel, completion order, current draft/session/list reentry, overlay comparers, partial exceptions and captured SynchronizationContext.
- [x] Mechanically transfer bodies into controller; thin same-service adapter and host forwards. Preserve ConfigureAwait(true), sync GetFull argument order and no-session-before-item async guard.
- [x] Targeted/full tests, GUI x64Release, independent new-range read-only review vs83a44cf. Fix introduced defects with RED/GREEN. Do not reopen completed codec review.
- [x] Record physical reduction, exact checks, inherited behavior rulings and remaining native/live-backend limitations; scoped local checkpoint.

Verified: CS0246 absent-controller RED;32targeted/full1330, GUI0warnings/errors41.01s. Independent manual_strategy_test_review ACCEPT, no findings, real service/host/item/state callbacks inspected. Added5coveragecases for empty/failed/partial/success actual evidence projection and returned-pair replacement/live list edits by observer; finalfull1335/1335 nofailed/skipped9s. These five test-only cases added after reviewed snapshot; product unchanged after GUI/review. PublicResult319->268physical lines, controller87/adapter26. Scope rulings: preserve inherited sync no-session failure, stale-item/current-list application after Load, backend-owned cancellation, completion order and partial mutations without rollback. Cost: no stronger cancellation/lateness guarantee; live runner/network/component recovery and interactive controls remain unexecuted. No second codec review.

### Task 30: Actual manual strategy test composition with inert backend

Product25e1db7042707d752cea35d4cdfb5317fe54eae2. Derive NEW cdpiui-manualtest-25e1db7-20261109 from Task28 harness; preserve prior31groups and add5runtime groups (36total). Keep public model/controller/operations/evidence/state/preset-builder/localizer exactpin. Add eleventh explicit boundary ONLY in AddOns BlockCheckManualStrategyTestService: fixture-only static typed delegate and replace TestWithCdpiuiAdaptersAsync runner creation/execution target with delegate invocation (throw if absent). Pure GetFullStrategyArguments remains actual writer call. Never invoke service.TestAsync/real strategy/probe runners/network/components. All prior10boundaries unchanged after root/pin substitution.

- [x] Five groups: actual ctor identity/callback binding and pure full arguments; unavailable result before item/backend; controlled pending result through public forward->samecontroller->sameadapter->servicehook, verify options/token, UI thread notifications and actual state rebuild/localized issue; Load while pending applies to same list/stale item/current session; fault/cancel/overlap order with bounded task release and cleanup. Literal output/actions/issue codes independent of implementation calls used as oracle.
- [x] Add seventh bootstrap ManualStrategyTestHostCases.cs. Sourcechecker exact4roots/pin/11boundaryfiles/7bootstraps/emptySecret; preserve alloldcopies. Fresh-path/root/asInvoker/process/deadline guards retained.
- [x] Build0warnings/errors; independent safety/assertion review before first execution. Runtime requires36PASS/COMPLETE/exit0/noFAIL/stdout/stderr, postsourcecheck, finishedprocess and unchanged prior3input bytes. No native-control/live-backend claim.

Verified: independent safety/assertion ACCEPT1208files/exact11boundaries/sevenbootstraps+emptySecret, additional buildinputs exactpin. Build0warnings/errors79.92s. PID14584 start08:50:40.3342330Z, exit0end08:50:43.1441970Z:36PASS/COMPLETE/noFAIL/stdout/stderr. DLL9A619EEA572D498CCD9B0ED5CF4FE7E69F39488C38D0F935139A344DC11A5371. Samecontroller/adapter/model/state/localizer/service-writer composition confirmed on actual DispatcherQueue; external service target inert, real TestAsync/runners nevercalled. Threeownbininputs byteidentical retained, postsourcecheckPASS, processfinished. No productchange. Preserve native15/16 gates and allpreviouscopies.

API syntax inventory refreshed f39e8a0->25e1db7 using same Roslyn runner/previouslyvalidatedcontrols:451originalexportedtypes,517->569GUIC#files,0removed/changed compared signatures. Only source syntax; no ABI/inheritance/semanticattribute/runtime guarantee.

Updated joint freeze: GUI25e1db7042707d752cea35d4cdfb5317fe54eae2 + committed security80c17c11be80c708d5985533dee8683781ee8e7f vsf39, fresh cdpiui-joint-80c17c1-20261109. Securityowner967/967 documented, laterWIPexcluded.1534/1534 nofailed/skipped75s; GUI0warnings/errors68.84s. Sameclean/pin/source-no-overlap/additive-test-items guards. Originalworktrees and allpreviouscopies preserved. Durablejournal/globalmutex/handoff/restart/MSI/Storepromotion remainseparate.

### Task 31: ConfigMaker variable actions

Base2295bbd, product25e1db7. Executor accepts this bounded extraction under delegated autonomy. Preserve all variable/document formats and actual host callbacks; do not run real dialogs/files/components/network.

- [x] Characterization with real document/variable/collection/content events and fake host. Absent-owner API RED; cover null/cancel/pending, reference equality/current membership, ordinal rename/replacement/reentry index timing, current command reads, removal confirmation/live object, insertion evaluation order, partial exceptions.
- [x] Transfer four workflows into ConfigMakerVariableActions + host port. Modify original Variables partial, keep RunReportingErrorsAsync/CreateVariableCommand/private forward and exact localized confirmation.
- [x] Targeted/full tests, GUI x64 Release and independent new-range read-only review vs2295bbd. Fix introduced Critical/Important with RED/GREEN, log deferred minors and inherited rulings.
- [x] Record reduction/checks/limitations and scoped local checkpoint. Assess actual host runtime proof separately; no routine refresh of unchanged joint/API snapshots.

Verified: absent-owner CS0246 RED, 38targeted PASS, full1373/1373 nofailed/skipped14s, GUI0warnings/errors45.29s. Independent variable_actions_review ACCEPT: exact transferred bodies, actual document/editor/Attach/tree/diagnostics/dialog/localizer/errorwrapper/XAML command bindings, no Critical/Important/Minor. Nullable enableannotations begins only below public forwards, preserving their original annotation context. Variables partial84->36, ConfigMaker aggregate1158->1112, owner85.

Executor rulings on every set-aside line: preserve inherited pre-event index despite reentrant move/clear (cost: same wrong-row/partial failure possible for such observers); preserve late-dialog Edit membership/Create current collection/Remove original-object behavior (cost: no new stale-operation cancellation); preserve real adapter late OriginalName evaluation (cost: outside name changes while dialog awaits may affect replacement as before); preserve partial callback changes and outer logger/status wrapper (cost: no rollback). Actual native dialogs/queued lifecycle/editor or Attach external IO/logger/status/visual result unexecuted; source wiring and compile are evidence for mapping, not runtime proof. No mandatory review fixes or deferred new minors.

### Task 32: Actual ConfigMaker variable host composition

Product c480fd3cc218526da792ac2c9b3a7b982ae8a584. NEW cdpiui-variables-c480fd3-20261109 derived from Task30 harness, previous36+five groups=41. Same11product boundary files, eighth bootstrap VariableHostCases.cs. Actual ConfigMaker host/owner/port/document/variable/command/localizer/tree/diagnostics unchanged from pin. Only bootstrap View gains typed Edit reply hook and Show tool action, both THROW if unset so previous unexpected-call guards remain. No new product boundary.

- [x] Five groups: ctor/same document/collection/host bindings plus canceled edit; real pending rename/dialog identity/editor/reference/document events; referenced removal canceled/accepted with exact localized confirmation and retained command plus actual CFG004 localization; insertion through real cursor/editor and read-only guard; actual CreateVariableCommand pending/CanExecute events/repeated execution suppression and Load while waiting adds same result to current document. Fresh unique variable names, environment-presence precheck only for expected diagnostic, no reading/output of other env values.
- [x] New cases use plain variables/string values and same document-host fixture component, no files/resources; previous three own-bin inputs retained. Track pending tasks/gates, finally bounded release/observe and CancelBackgroundWork to stop actual diagnostics timer; preserve primary failure. Do not exercise error wrapper failures because production logger/status external targets remain real.
- [x] Fresh sourcechecker all4roots/pin/exact11boundaries/eightbootstraps/emptySecret/buildinputs; build0warnings/errors, independent safety/assertion review before first execution. Require41PASS/COMPLETE/exit0/noFAIL/stdout/stderr, postsourcecheck/finishedprocess/byteidentical3inputs. No native-dialog/visual proof claim; native15/16 stayopen.

Verified: independent safety/assertion ACCEPT1210trackedfiles/exact11priorboundaries/eightbootstraps/emptySecret/pinnedbuildinputs. No mandatory fixes. Build0warnings/errors63.59s. PID34304 start09:20:48.8343188Z exit0end09:20:52.1280114Z:41PASS/COMPLETE/noFAIL/stdout/stderr. DLL82089C1516BAEB1E5A4BB8E49CAA8908A208F96450D903EB4E8375D9274E4BE9. PostsourcecheckPASS/processfinished/3inputbytesretained; runtime-createdbinentries only3inputs+resultlog. Real variable model/commands/editor/diagnostic/localizer composition passed, controlledviewtargets, no new productchange. Review rulings: first scratchrunlog absent despite notseparatelylistedguard, allotherfreshpathsblockrerun; no change needed for first reviewed execution. Logger reachable on unexpectederrors but exactexe/data-root guard boundsnormaldata effects to ownbin; not runtimeproof of logger/error/nativepaths or arbitraryexe isolation. Native15/16 open. Preserve everypreviouscopy and evidence.

### Task 33: BlockCheck2 run lifecycle controller

Base e890887, product c480fd3. Executor accepts bounded extraction described above. Move only orchestration/CTS/final-state decision; backend/history/cleanup and state/display remain host. Do not alter SiteLists/Storage or run actual deletion/network/history in tests.

- [x] Write real-domain/fake-host behavior tests, absent-controller API RED. Cover ordering/identities, CanStart/Cancel guards, prelude outside try, invalid/final-state mapping, faults/OCE, pending run/history/report/final-state capture, CTS disposal and new-run reentry, callback/finally failures, original SynchronizationContext.
- [x] Move bodies and CTS into owner, preserve public API and exact host steps via explicit narrow adapter. Keep RunPrepared/SaveHistory/State/Notify/progress/result implementations unchanged, only corrected ownership comment if necessary.
- [x] Focused/full suite and GUI x64 Release; independent read-only new-range review vs e890887, resolve introduced defects with RED/GREEN, record inherited/deferred rulings.
- [x] Scoped local checkpoint and clear runtime limitations; assess separate actual-host proof before code freeze. Winddown10:45UTC and deadline11UTC unchanged.

Verified: absent-controller CS0246 RED,44focused PASS, full1417/1417 nofailed/skipped14s, GUI0warnings/errors43.98s. Independent run_lifecycle_review ACCEPT: exact orchestration/CTS/final-state/adapters/options/progress/results/service/history/cleanup source composition, no Critical/Important/Minor. No product changes after GUI/review. BlockCheck aggregate607->551physical lines; owner98.

Executor rulings on all eight inherited/set-aside items: preserve prelude outside try/no-finally on guard/reset/start failure; prepared Targets/Issues/events before Success and partial callbacks; CTS before Running and result-owned cancellation outcome; captured final state/current report before awaited history and blocked normal new run; actual history wrapper swallows ordinary service IO errors (HistorySaveError+Logger), unlike fake endpoint fault; ReferenceEquals protects only new CTS on final-state reentry while old Finish/shared progress and cleanup/current root still run (cost: existing new-estimator/root interference possible); catch/finally failures retain original partial mutations/resource boundaries; synchronous Cancel uses current field after state callbacks with no new synchronization. Costs explicitly retained, no claim of new rollback/lateness/cancellation/cleanup guarantees. Native/backend/history/logger/cleanup/deletion execution and actual-host integration remain unproven by unit/source checks. No mandatory fixes/deferred new minors or reopened closed reviews.

### Task 34: Actual BlockCheck2 run host composition

Product26297fdf386f7c09f4d058486399d4ac7318937f. NEW cdpiui-runlife-26297fd-20261109 derived from Task32, old41+fivegroups=46, ninth bootstrap BlockCheckRunHostCases.cs. Same11priorboundaries plus TWO narrowly patched product files: BlockCheck2ViewModel.cs changes only low-level options-settings factory, Prepare call target (retains actual CreateSessionRequest), RunWith backend target (retains actual Progress/options/preflight merge), History Save target and Logger target; SiteLists partial changes only private CleanupGeneratedSiteLists target to recorder. No Storage changes/deletion or real network/history/database/settings. Actual ctor/owner/port/options/progress/results/state/report builder/serializer and history wrapper bodies retained except listed targets.

- [x] Fivegroups: actual ctor/controller/host/options bindings and guard/invalid preparation with recorded cleanup; pending backend progress on DispatcherQueue and Cancel -> canceled result/report with no deletion; pending history keeps CanStart false/CTS alive until final, actual preflight merge/result/session/report/serialization; synthetic history IO fault reaches actual HistorySaveError+inert Logger and still final Completed; synthetic backend fault/OCE reach real OperationError/Failed vs Canceled/null and recorded cleanup. Use pure ManualHostSession data and memory-only settings provider.
- [x] All callbacks record data/threadflags without throwing assertions inside controlled workflow; assert outside tasks. Track backend/history gates/tasks and release with WasCanceled result / memory history reply, bounded observe in finally, stop progress; remove subscribers/reset hooks. No actual RunWith/Prepare/Save/Logger/cleanup IO fallback allowed.
- [x] Sourcechecker all4roots/pin/exact13boundaries/ninebootstraps/emptySecret/buildinputs, build0warnings/errors, independent safety/assertion review before firstexecution. Require46PASS/COMPLETE/exit0/noFAIL/stdout/stderr/postsourcecheck/processfinished/retained3inputs. Real production endpoints, reentrant generated-root safety, native controls remain unproved.

Verified: initialfixtureCS8752 params targettypednew RED29.86s preserved, onebootstrap typename correctedexplicitly, no productchange. Corrected existingcopybuild0/0 43.16s, independent safety/assertion ACCEPT1212files/exact13boundaries/ninebootstraps+emptySecret/buildinputs. PID44184 Start09:40:59.7214656Z Exit0End09:41:02.1449988Z:46PASS/COMPLETE/noFAIL/stdout/stderr/postsourcecheckPASS/processfinished/3inputsretained. DLLC89BCAC4EC99452CC9FF82590C4D824BDE2C370E2FEF4C7CFA871824A28C2E95. Actualhistorywrapper policy and progress/result/controller composition verified with inert endpoints, no real network/storage/settings/logger/deletion/reentrantroot/nativeclaim. Reviewlimits adopted, first-run pathscheckedabsent including scratchcopy, do notrerunarchivebuilder/prepare/runner. Native09:39read confirmedunchangedconsent3803510 ToolsSmokeTestunknownpublisher/UIAonly0, no inputs,15/16open.

### Task 35: Compiled public contract verification

Executor accepts additional regression evidence for original exported GUI surface, without modifying product. Build pristine f39e8a0a7267e4fcd4a02eef186a3ee08df8dcd9 in NEW short cdpiui-abi-f39-20261109, only exact generated empty Secret allowed; sourcecheck all4roots/buildinputs. No application execution. Compare compiled baseline CDPIUI.dll to product26297fd GUI DLL from verified root build using standalone net8 System.Reflection.Metadata/PEReader tool (BCL only, no new package).

Evidence-driven scope adjustment: first compiled comparison at26297fd returned16changed shapes, all from public methods/getters implicitly implementing new internal interfaces and receiving virtual/final/newslot flags. Types/parameters remain unchanged and sealed types remain non-overridable, but prior public metadata transparency is stricter. Preserve RED artifact. Add explicit private interface forwards to original public members in five files (storage service, ConfigMaker Document/Files/Variables, BlockCheck main), no method-body/format/policy change. Verify full/GUI and compiled GREEN plus independent new-range review before checkpoint; this bounded product correction replaces the initial no-product-change assumption.

- [x] Fresh scratch analyzer/inputs/logs and guarded archive/build; verify rootpin/sourceunchanged/currentDLL identity and same SDK/config/platform. Preserve all prior copies/evidence.
- [x] Canonical metadata signatures for exported types and externally accessible public/protected members: base/interface retention, visibility/type/static/virtual/abstract/sealed relevant flags, method/field/property/event types, generic constraints, parameter names/defaults, literal/enum constants. Decode type signatures by names, not unstable metadata tokens; permit additions. Record exact limitations: no complete annotations/attributes/interop/behavioral/native-ABI guarantee, compiler implementation attributes excluded.
- [x] Positive identical-assembly control and compiled synthetic negative controls independently exercise signature/default/setter/constant/base/constraint/flag removal detection. Meaningful expected failing exit, then product comparison. Independent read-only analyzer/assertion review before reporting proof.
- [x] Refresh source inventory and latest committed-security joint compatibility once code freezes; then requirement-by-requirement audit/status/rulings before10:45UTC winddown and11UTC deadline. Do not declare native15/16 or real external endpoints complete.

Verified: pristinef39 baseline1374trackedrepositoryfiles exactLF before/afterbuild, onlyemptySecret, GUI0/0 57.32s. SameSDK8.0.427/Release/x64. Initial normal262DLL8148ECB5... RED16shapes; explicitforwards correction full1417/1417 nofailed/skipped15s+GUI0/0 45.44s. CandidateDAB8100F612A9E26DB93BFDA207DBB6CD19BF32E61BE0EE05681A08F5383057B:452originalexportedtypes/452after,0removedchangedshapes,7addedinternal-interface declarations. Identical/recorded-shape-positive controls0; negativeexit2/16changes andstrict11categoryassertions. Independent compiled_contract_review ACCEPT noCritical/Important/Minor, compared identical baselineinventories andonlyexpected16restored publicflags; productendpoints/taskidentity preserved.

Rulings: keep strictvirtual/final/newslot ratherthannormalize away; positivecontrol deliberately changes output/timing and proves SHAPEequivalenceonly; defaults/constants onlyConstanttable, attrencodeddecimal/DateTime/params/callerinfo/NRTetc excluded; comparedeclaredmembers/directbase/interfaces, notdependency/inheritedclosure/MethodImplmaps, assemblyversionsignored; no completeABI/interoplayout/marshalling/attributes/behavioral/no-regressionguarantee. No further polish/productcorrection requested. Preserve allRED/GREEN binaries/json/logs; normalproductDLL distinctfromruntimefixtureC89. Final46actualhostcomposition atnewpin and source/joint refresh remain next.

### Task 36: Final freeze verification and completion audit

Product74659b74e3d53a0e024bfe6075169901c554f2de, normalDLLDAB8100F612A9E26DB93BFDA207DBB6CD19BF32E61BE0EE05681A08F5383057B. No new features planned. Repeat actual46groups in NEW cdpiui-final-74659b7-20261109 using same13fixture boundaries/ninebootstraps, allowing only newpin/root/log substitutions; explicit privateport mappings remainexactpin. Previous262fixture preserved. Narrow independent delta/safety review before firstexecution; no reopening closed body decisions.

- [x] Newfixture allsource/buildinputs checks, build0warnings/errors, independent delta/safety ACCEPT,46PASS/COMPLETE/exit0/noFAIL/stdout/stderr/processfinished/postsourcecheck/inputbytesretained. Record actualport mappings exercised and realexternal/native limits.
- [x] Refresh originalsource syntax inventory f39->74659, preserve oldcontrols/evidence, and normalDLL/compiledcontract snapshot pin/hash guards.
- [x] Latest documented committed-security joint pin (currentread ee47716, owner980/980) in freshcopy, clean/source-no-overlap/additive-testitems guards, fullsuite+GUI. Do notincludeWIP/editparallelworktree.
- [x] Audit original user scope, audit recommendations asdata, approved order, every plan gate/invariant/artifact and all review rulings against current authoritative evidence. Distinguish completed bounded passes from unhandled large classes/global NRT/security/native gates. Record exact latestpins/results/remainingrisks, architectureownership/map if useful, final cleanbranch/localcommits/no publish/no destructive source actions.
- [x] Begin winddown at10:45UTC; no further product changes or new refactoring stages. Preserve current evidence and prepare final self-contained Russian report. Completion requires bounded user objective audit; native15/16 remainopen.
- [x] At11UTC stop the timed run and deliver the report, recording the actual remaining native blocker rather than declaring full completion. No new work past the deadline. Delivered in chat at14:00MSK; native15/16 remainedopen. Later explicit user request resumes work without that deadline.

Freeze746 evidence: actualfinal46PASS/COMPLETE/exit0 atPID40860,10:07:17.2076428Z->10:07:19.5379863Z, source1212/13boundaries/ninebootstraps/emptySecret, build0/0 63.63s, independentdelta/safetyACCEPT,3inputsretained. Source451types517->573files0changedsignature; jointGUI746+securityee4771675956fe213634675f37579362efb7e0eb owner980:1629/1629 nofailed/skipped92s, GUI0/0 64.27s. Allhistoricalevidencepreserved. Completionaudit identified approved nextlargeVM CreateNew1037, one bounded pure draft extraction below before winddown, then refresh latestchecks; no new feature/policy.

### Task 37: CreateNewConfig condition draft ownership

Baseb1eb2ca, product74659. Exact bounded owner described in spec; otherlargeVMs remain backlog and Welcome migration stays outside parallel ownership. No save/import/testing/Logger mutation. Source-link real ConditionVariableModel, preserve annotations-only context.

- [x] Real-model/collection/spies behavior tests before owner (absent API RED): setters/equality/null/notification order, validation/preview current reads/shortcircuit/localization, reset redundant Preview/order, Add invalid/raw/exit-before-read/reentry/identity/partial errors, real Core formatter.
- [x] Move draft state/logic into store-only owner, plain host public forwards and same sender, lazy production callbacks. Keep original collections and unrelated methods. Full/GUI, compiled and source contract retention, independent new-range read-only review.
- [x] Checkpoint then finalsource/compiled contract+joint/runtime delta only as justified; no new code after10:45UTC. Record inherited rulings and untouched scopes. Native15/16 remainopen.

Verified: absent API CS0246 RED,24focused/full1441/1441 nofailed/skipped13s, GUI0warnings/errors42.20s. CompiledcandidateDAFB35EC6A4CCD6B728A4D05FB1712465C79420A3B38633B0F199628BA02552B vsf39:452types0removedchanged7addedinterfaces. Independent condition_draft_review ACCEPT allbodies/actualhelpermapping/XAMLhostsender/modelcontext, noCriticalImportantMinor. One expected formatter literal corrected from source beforeGREEN; no productalgorithm repair. CreateNew1037->1001physical lines,Variables118->74, owner84. No productchangesafterGUI/review.

Rulings adopted: retainASCIIvalidator/space-onlypreview normalization/whitespaceOnOff; rawinvalidduplicateAdd/noreset remainsbaseline; Remove/VariableChanged/Logger/Save/Import/Test unchangedandnotnewexecutionproof; actualWinUIctor/localizer/render/flyout/native notprovedbyunit/source; inheritedlargeclass/globalNRT/security scopesopen. Existingmodelannotations-onlycontext preservedbydirectives, no UIstubs. Next actualCreateNewhost group canuse realconstructor (onlycommands,HardcodedItemIds pure dictionary,localizerstrings) with fakeICreateNewConfigView.SetExitAskAvailable, everyotheractionthrows, no newproductboundary.

### Task 38: Last validation of condition host and final artifact audit

Producte42ac481ab230866bd65ea06ccb7496a52a75c9c, normalDLLDAFB35EC6A4CCD6B728A4D05FB1712465C79420A3B38633B0F199628BA02552B. NEW cdpiui-complete-e42ac48-20261109 fromTask36, same13boundaries plus tenthbootstrap CreateConditionHostCases.cs, old46+5groups=51. NO newproductboundary: actualCreateNewctor/host/owner/model/helpers/localizer exactpin. Constructorcalls onlyRelayCommands/HardcodedItemIds dictionary/localizedstrings, fields/defaultcollections; noOpen/Select/Save/Test methods. FakeICreateNewConfigView onlySetExitAsk records, everyotheroperationthrows.

- [x] Fivegroups: actualCtor/sameowner/collection/noViewcalls; realvalidation/Corepreview/hostsender/order/invalidlocalizedfallback; reset order/redundantpreview/noexit; Addexit-first currentrawvalues/freshmodel/invalidallowed/sharedcollection; hostreentry and synchronousobserverfailure withassignedfield/no derivedevents. No nativecontrols/flyout/externalIO/logging.
- [x] Build/sourcecheck1213tracked/13boundaries/10bootstraps/emptySecret/buildinputs; independent delta/safety review before firstexecution;51PASS/COMPLETE/exit0/noFAIL/stdout/stderr/postcheck/processfinished/3inputsretained. Source/compiledcontracts atnewpin and latest tested-security ee47716 joint freshcopy/full+GUI.
- [x] Complete Task35/36/37 verification/audit checkboxes according to actualscope, keep native15/16open; audit/map/reviewrulings/backlog/source-vs-audit citations accurate. No furtherproductchanges; winddown begun10:45UTC, deadline/report gate remains explicit underTask36.

Verified e42: NEW complete-e42 fixture build0warnings/errors59.36s, independent safety/assertion ACCEPT1213product-root files/1457fullarchive/13boundaries/10bootstraps/emptySecret. PID42532 10:26:59.2287230Z->10:27:01.2864720Z exit0,51PASS+COMPLETE/noFAIL/stdout/stderr, postsourcecheckPASS/processfinished/3inputbytesretained. RuntimeDLL814BB17DAF15F1B930C1FFCAFB472DD11C7F13325D92FFB4697EFD0AF1AE0733; normalDLLDAFB35... retained separately. Source451types517->574files0removedchanged; compiled452types0removedchanged7addedinterfaces. Fresh joint e42ac481ab230866bd65ea06ccb7496a52a75c9c+ee4771675956fe213634675f37579362efb7e0eb1653/1653 nofailed/skipped38s GUI0/0 62.55s. Evidence paths, ownership map, inherited rulings, audit references and remaining scopes consolidated in docs/DECOMPOSITION-ACCEPTANCE.ru.md; no product change after reviewed e42.

### Task 39: CreateNewConfig test session ownership

Base63ce869, producte42. Spec section «Возобновление: test session CreateNewConfig». New ViewModels/CreateConfig/CreateConfigTestSession.cs; Helper/CreateConfigHelper/ICreateConfigTestTasks.cs and CreateConfigComponentTestTasks.cs; modify existing main constructor and Testing partial; Presentation/CreateConfigTestSessionTests.cs + additive source links. No new host partial files.

Interfaces: ICreateConfigTestTasks event Action<Tuple<string,bool>> StateUpdated, Task StopAsync(string), Task RunAsync(string,string). Session constructor takes tasks, Func<(bool HasComponent,string ComponentId)> selection (distinguishes no selection from null/empty id), Action convertDesigner, Func<ConfigItem?> createConfig, Func<ConfigItem,string> startupArguments, Func<string,string> localize, Action<Action> enqueue. Exposes ToggleAsync, Stop, InitializeText(no event), button forwarding properties and readonly state for focused assertions. Host sender preserved; adapter lazy.

- [x] Write real-owner/ConfigItem managed tests first: store-only init, start/stop/subscription/order, missing/null/empty selection, pending stop/current reads, queued events/case/current-id, natural exit/resubscribe, fault/partial-state/fire-and-forget, reentry/localizer/observer failures. Run absent API RED; expected CS0246 only for new types.
- [x] Extract exact lifecycle with narrow callbacks and lazy adapter; preserve original host signatures/private setters/command and queue behavior. Focused tests GREEN; full suite and GUI0warnings/errors with SDK8.0.427, git diffcheck.
- [x] Compare normal DLL compiled surface and source signatures as justified; independent review once for new range only, rule on every finding, RED/GREEN mandatory corrections. Checkpoint scoped files, record counts/pins/logs/rulings and remaining host/native gaps.

Verified96c2cfe782b329354b19a90e9129fa2b8d0bc852: absentAPIREDCS0246,33focused/full1474/1474 nofailed/skipped9s, GUI0/0 41.72s; source451/517->577/0, compiled452/0+7 unchangedinterfaceadds. NormalDLL E9407FAB802C6663440612F3814362708505172975838C53BFB7BE41598A8EEC. IndependentfreshreviewACCEPT noCritical/Important, twoMinor managedunitproofgaps deferred (prelude running assertion inside subscription hook; Stopfireforget vs completedTask). Task40 actualhost plannedprelude snapshots/returnedfaultedTask observation will supply additional integration proof, no productcorrection/re-review. Reviewrulings/costs inledger. Host1001->929, Testing118->24, owner129; public/privateaccessors/XAML/leavepage hook preserved. No realmanager/IO/native execution.

### Task 40: Actual CreateNewConfig test host composition

At96c2cfe prepare NEW cdpiui-createtest-96c2cfe-20261109 from existing51group fixture, retaining priorcopies. Same13priorboundaryfiles plus TWO narrowly cut files: CreateConfigComponentTestTasks.cs replaces four manager.Instance targets with configured inert tasks endpoint; CreateNewConfigViewModel.cs replaces only CreateConfig callback and startupformatter callback targets with memory-only functions. Actual owner/adapter/constructor/commands/queue/property forwards retained. Rationale: actual CreateConfigConditionBuilder resolves/creates/reads locale even saveLocalization=false, CreateConfigPageHelper uses DatabaseHelper, formatter invokes LScriptCore unsafe engine; do not execute them merely to test session composition. Actual ConvertDesigner remains, with only unsupported fixture component IDs so existing guard returns before editing/IO. No live manager/process/pipe/settings/DB/locale/Logger/native operations. Total15boundaries, eleventhbootstrap CreateTestHostCases.cs,51+fivegroups=56. Sourcecheck all productroots/fullarchive/buildinputs and exactcuts; build/safety review before first execution. Native15/16 remain separate.

Five groups: constructor owner/realadapter/commands/initialtext/noendpointcalls; actual AsyncRelayCommand awaits controlledStop with prelude flag recorded on firstremove/add and current configuration read afterawait, RunTasknotawaited; actual DispatcherQueue receives background state with hostsender/thread/order/case/lateid/naturalexit/resubscribe; actual StopTestRun resets/doubleunsubscribes and returns normally with faulted returnedTask observed independently; commandfault/nullconfig preserve ownerpartialstate plus commandfinally and no unsafe error endpoints.

Every pending stop/run/queue task bounded and released/observed in finally; stop each model before changing configured endpoint, detach host observers and drain queue before clearing hooks. No assertions thrown inside controlled backend callbacks; record and assert afterward. Existing51groups preserved unchanged except newpin/root/log and one call/count. Initial run only after independent safety/assertion ACCEPT, then56PASS/COMPLETE/exit0/noFAIL/stdout/stderr/postsourcecheck/processfinished/threeinputsbyte-identicalretained.

- [x] Specify new boundary delta and five meaningful actual-host groups, including host sender, initialization and queued callbacks/command awaits with controlled tasks; safety/cleanup proof before run.
- [x] Build/review/execute once, preserve earlier fixtures and logs; update status/acceptance with current pin and no inflated native/production claims.

Verified: freshfixture1216productroot/1462fullrepo/15cuts/11bootstraps+emptySecret/noextrabuildinputs; build0/0 69.69s; independent safety/assertion ACCEPT beforelaunch, EXEasInvoker/uiAccessfalse E17175E... andfixtureDLL E29FF54F... matched. PID5764 12:59:28.7341308Z->12:59:30.9999152Z exit0;56PASS+COMPLETE/noFAIL/stdout/stderr/postsourcecheckPASS/processfinished/3inputbytesretained andfreshhashJSON. Constructor event observer attachesafterctor, so runtimeprooflimitedtoinitialvalues/lazyendpoint/view; queuedcross-ID-change coveredmanagedonly, runtimechecksbackgrounddelivery/lateoldid. Unexpectedtrackedfault canshortcircuit cleanup afterallgatesreleased; ownappfails/exits/no realendpoint/no nextgroup, notgeneralcleanupguarantee. Allreviewlimits/rulings recorded. Fresh joint96+EE1686/1686 nofailed/skipped38s GUI0/0 57.16s; oldfixtures/evidence preserved.

### Task 41: CreateNewConfig graphic designer state

Basec4cd534, product96c2cfe. Spec section «Возобновление: состояние graphic designer CreateNewConfig». New ViewModels/CreateConfig/CreateConfigGraphicDesigner.cs, Helper/CreateConfigHelper/ICreateConfigGraphicDesignerBackend.cs and CreateConfigGraphicDesignerBackend.cs; modify existing main constructor/AdditionalSettings and GraphicDesigner partial only; new Presentation/CreateConfigGraphicDesignerTests.cs + additive source links. No codec/model/Core/DB/security implementations change.

Interfaces: backend LoadGoodbye(items,groups), LoadSpoof(items,groups), bool TryLoadNoDpi(string id,items,groups), string Parse(items,groups,string), string Build(items,groups,string), all existing ObservableCollection model types. Owner receives same collections, backend, Func<Components,string> live component-id getter. Owns AdditionalSettings, LoadedComponentId, Load(string), ChangeText(Tuple<string,string>), ToggleBool(Tuple<string,bool>), SelectGuid(Tuple<string,string>), Parse(string), Build(). Host forwards original handlers/properties and same sender; UI branch/order/editor normalization unchanged.

- [x] Write real production model/collection behavior tests before absent owner/port: store-only constructor, AdditionalSettings/equality/null/events, known/cache/reload/case/missing annotation/current id/reentrant/throw load, first-match text/bool/group GUID edits and grouping/partial errors, parse/build live callbacks/argument capture. Run expected absent API RED.
- [x] Extract exact state/algorithms into owner and thin lazy backend; keep UI policies and public/private host surface. Focused GREEN, full suite, GUI0warnings/errors with SDK8.0.427, compiled/public source retention and diffcheck.
- [x] Independent review new Task41 range once; rule inherited policies, defer minors, fix mandatory defects via RED/GREEN. Scoped checkpoint/counts/status/evidence, no claim real NoDPI IO or native controls.

Verifiedcdf3bdc91c35d473128caba39fc1ae6672ef0152: absentAPIREDCS0246,34focused/full1508/1508 nofailed/skipped10s GUI0/0 38.02s; source451/517->580/0, compiled452/0+7oldinterfaceadds. NormalDLL D3E078495398F2C896D0F37AE9B72B07F74FFC949FF35C990404AC0B53E825B9. IndependentfreshreviewACCEPT noCriticalImportant; oneMinor tabletheoryself-validation weakness deferred, othercasesalreadycoverdispatch. Allreviewrulings/costs recorded. Host929->858; GraphicDesignerpartial135->61, owner109. No productchangeafterchecks.

### Task 42: Actual CreateNewConfig designer composition

After Task41, NEW pinned copy from56group fixture, same15boundaries plus narrow new designer-backend NoDPI DB/exists/XML targets only. Actual owner/host/collections/commands/AdditionalSettings/real safe factories+codec and tab sequence retained; new five groups =>61 and twelfth bootstrap. Define exact source cuts/assertions/cleanup before editing. No real DB/XML/component/native/shell/save calls; no changes in prior cases beyond pin/root/log/call/count.

- [x] Specify/build/sourcecheck allarchive/buildinputs and independent safety/assertion review before first execution; five groups prove actual wiring/current models/view sequence/commands/cache/real safe parse-build and NoDPI retry on inert endpoints.
- [x] One bounded own asInvoker run with61PASS/COMPLETE/exit0/noFAIL/stdout/stderr/postcheck/inputbytesretained; current-pin joint/security compatibility once, preserve all previous copies. Record exact scope, counts, limits/rulings.

Verified: freshfixturecdf3bdc1219productroot/1466fullrepo/16exactcuts/12bootstraps+emptySecret/noextrabuildinputs, build0/0 73.28s, independent safety/assertion ACCEPT beforelaunch. EXEasInvoker/uiAccessfalseE17175E...; fixtureDLLD32996BFF216FD32FFCC82A707FED143B21AA4431AB4A69E090F17C1DB96692A matched. PID33316 13:25:40.5160465Z->13:25:42.7032211Z exit0;61PASS+COMPLETE/noFAIL/stdout/stderr/postsourcecheckPASS/processfinished. Finalexternalbytecheck3inputsPASS/hashJSONretained. Spooffactoryidentity sourceprovedonly, runtimegroup population/liveRead/guard; no ctor-eventabsence/nativecontrols/realNoDPIIO claims. Freshjointcdf3+EE1720/1720 nofailed/skipped37s GUI0/0 56.35s. Alloldcopies/evidencepreserved; reviewrulings/minors explicitinledger/status.

Task42 refinement before fixture editing: productcdf3bdc, NEW cdpiui-designer-cdf3bdc-20261109 (short path to retain WinUI/PRI path margin). Same15previouscuts plus one new file CreateConfigGraphicDesignerBackend.cs, exactly three NoDPI targets: directory lookup, File.Exists, XML_LoadDesignerConfig -> memory-only guarded recorders (no actual file). Keep real Goodbye/Spoof/Parse/Build helper calls. Same11previousbootstraps/case sources unchanged; add CreateDesignerHostCases.cs as twelfth, main onecall/count61/root/pin/log substitutions only. Total16boundaryfiles. Actual application remains asInvoker/no native windows/input.

Five groups: actual constructor/owner/backend/collection identities and AdditionalSettings host sender/equality/reentry/observer fault; actual Goodbye factory + selected/tab/view order/cache + known -p/port parse/build; actual three RelayCommands against real mutable models/grouping/first-match/reentrant values; real Spoof factory and host current EditorText read after controlled normalization-write reentry plus unsupported-component visibility/convert guard; NoDPI missing/retry/success/cache and propagated controlled loader fault via inert directory/exists/XML endpoints. Constructor notification absence remains source/unit proof (observer attaches later); fake view is not real TabView/native UI. No actualSave/CreateVariables/test-run/DB/script/shell paths, prior56tests retainTask40cuts. All callbacks record rather than assert; controlled expected fault caught in scenario, observers detached/hookscleared in finally. Safety/assertion review after build and before firstexecution;61PASS/COMPLETE/exit0/noFAIL/stdout/stderr/postcheck/processfinished/final3inputbytes check. Freshjointcdf3+committedEE once, no WIP.

### Task 43: CreateNewConfig component selection coordinator

Base9985e52, productcdf3bdc. Spec section «Возобновление: component selection CreateNewConfig». New ViewModels/CreateConfig/CreateConfigComponentSelection.cs; modify existing main forwards/constructor/private CheckViewType/IsSaveAvailable and ConfigComponentOption.cs annotations-context only; new Presentation/CreateConfigComponentSelectionTests.cs + source links. No new host partial, navigation/Save/other-owner/backend/codec implementation changes.

Constructor: Func<string,bool> supported, Action selectDesigner, Action selectText, Action<bool> handleSelection, Action dirty, Action audit, Func<string?> editorText. Owns SelectedComponent(ConfigComponentOption?), DisplayName, IsAdditionalSettingsVisible=false, IsNotReadyErrorVisible=true, IsTabBarVisible=true; derived IsDesignerSupported/IsSaveAvailable; CheckViewType(). Same null/default identity and source order. Host proxy uses original declarations/private setters; no implicit interface port flags.

- [x] Real-model/delegate/spies behavior tests before absent owner: constructor/defaults, identity/equality/null/display normalization, exact supported/unsupported/null callback+event order, live capability/shortcircuit/whitespace/nullID, reentrant selection/flags/view effects and partial exception paths. Expected absent API RED (including source-link input as necessary), no GUI stubs.
- [x] Extract exact coordinator with store-only callbacks; preserve host private entrypoints/publicmetadata/notifications/queue interactions. Focused/full GREEN, GUI0warnings/errors SDK8.0.427, normal compiled/source contract retention, diffcheck.
- [x] Independent fresh new-range review once, mandatory fixes viaRED/GREEN only, minors/rulings explicit. Scoped code checkpoint/counts/status, no actual control/persistence claims.

Verified: absent API/source input REDCS0246,34focused/full1542/1542 nofailed/skipped9s GUI0/0 44.16s; compiled452/0+7 unchanged prior declarations. NormalDLL3F38926D...; independent fresh review ACCEPT0Critical/Important, one managed live-read coverage minor deferred to actual-host repeated query. Host858->818/main299->259/owner103. Source inventory451/517->581/0 at cbd16ad; Task44 runtime/joint pending.

### Task 44: Actual component selection and capability composition

NEW pinned copy afterTask43 from61group fixture, same16boundaries (Main two inert test-prep targets unchanged) and oldcase sidecars unchanged. Add thirteenthbootstrap fivegroups=>66. No new cuts/real services/native controls. Existingactual host/private CheckViewType Navigation caller/commands/view interaction retained; controlled view/observers only. Define exactfivegroups/cleanup before editing, review source/buildinputs/allarchive/safety before firstexecution.

- [x] Prepare/build/review five actual groups for cold owner/host forwarding, live CanExecute/query shortcircuit, supported/unsupported/null order and reentry/observer partial-state; same16sourcecuts/no hidden new endpoints.
- [x] Single own asInvoker66PASS/COMPLETE/exit0/noFAIL/stdoutstderr/postcheck/processfinished/final3bytesretained; currentpinjoint+committedEE once; report scoped proof and leave native15/16 open.

Task44 refinement before fixture editing: use one new short pinned copy, same16cutfiles and unchanged prior61case sources; add CreateSelectionHostCases.cs only as thirteenth sidecar. No command Execute, Save, test start/stop or new endpoint substitutions. Five actual-host groups: (1) same concrete coordinator/public defaults/cold command state and constructor lazy-view behavior (observer attaches later, so no runtime absence-of-constructor-notification claim); (2) both real commands query current editor, shortcircuit missing object/name, whitespace and null-ID distinction, no extra computed notifications, editor-read reentry retains earlier guards; (3) unified host-property/view/Save-then-Test audit trace for unsupported/supported/null selection, object identity and equal-value suppression; (4) observer/tab reentry with original outer continuation and partial flags; (5) controlled property observer, exit-ask, command-audit and editor-read faults preserve assigned/partial state and stop subsequent callbacks. Private CheckViewType stays source-verified for Navigation; direct supported/unsupported switching exercises its same owner target without navigation IO.

Controlled view records facts rather than asserts inside callbacks; optional hooks invoke only owned model changes/IOException. UI-thread and host/command sender checks; detach all model and command handlers, clear hooks in finally, verify test session and commands stayed cold. Unsupported markers avoid factory/DB IO; supported Goodbye uses the already-reviewed real safe factory/codec. NoDPI endpoints remain the same old inert cuts. Fresh archive/input checks, build, independent safety review, exact asInvoker hashes before launch; all66groups then process exit, post-sourcecheck and external literal-byte check for three own direct-child inputs. Old evidence/copies preserved, no native/deployment/real backend claims.

Verified: productcbd16ad/1220roots1468fullarchive/16cuts/13bootstraps/build0/0 122.24s; independent safetyACCEPTbeforelaunch. PID47540 13:55:16.6378597Z->13:55:21.2333556Z exit0/66PASS+COMPLETE/noFAIL/stdoutstderr/postcheck/final3literalbytes. NewjointCBD+EE1754/1754 nofailed/skipped1m41s GUI0/0 88.39s. All scope/rulings/status preserved, no productchange.

### Task 45: ConfigMaker diagnostics panel

After Task44 checkpoint. Spec section «Следующий проход ConfigMaker: diagnostics panel». New Helper/CreateConfigHelper/ConfigMakerDiagnostics.cs (internal generic owner), new Presentation/ConfigMakerDiagnosticsTests.cs; modify existing ConfigMakerViewModel.cs constructor and Diagnostics partial only, additive test links for owner/actual validation/reference-validator source. ComponentCommandValidationService.cs annotations-only context if needed; no service algorithm/UI row/formatter/resource/security changes.

Interfaces: constructor ObservableCollection<TItem>, Func<string> editor, Func<IReadOnlyList<ComponentCommandHelpOption>> options, IEnumerable<ConfigMakerVariableDefinition> variables, Func<string,bool> resource, Func<ComponentCommandDiagnostic,TItem> factory, Func<string,string> text, Action refresh. Rebuild(), ErrorCountText/WarningCountText internal owner setters and private host forwards, GetSeverityText(ComponentCommandDiagnostic), GetDescription(ComponentCommandDiagnostic). Host factory keeps original initializer order and calls owner text methods; same collection and property names. Timer Stop/Start/Tick, Cancel and existing private RebuildDiagnostics callers unchanged.

- [x] Actual-model tests before owner: lazy constructor/defaults, current editor/options order, Validate/reference ordering and results/order, all kinds/severities/token formatting/current culture, counts/equality, collection/factory/counter/localizer/highlight reentry and partial exceptions; no UI stubs/real IO. Run absent-owner RED.
- [x] Extract exact code, focused/full GREEN and GUI x64 SDK8.0.427 0warnings/errors; source/compiled retention, diffcheck. Preserve original public annotations/metadata/context.
- [x] Independent fresh new-range review; mandatory fixes one RED/GREEN pass, defer minors/rule scope. Record exact pin/counts/proof; actual-host next task, native15/16 remainopen.

Verified: missinggenericownerCS0246RED,37focusedGREEN/full1579/1579 nofailed/skipped15s GUI0/0 59.12s; compiled452/0+7normalDLLAE058413...; independentfreshreviewACCEPT0Critical/Important. Minor variabletestenvfallbackweakness deferred, actual-host distinct guarded marker planned. ConfigMaker1117->1075/Diagnostics96->48/owner90. Source inventory451/517->582/0 at36b9e23, actualhost/timer/runtime nextgate.

### Task 46: Actual ConfigMaker diagnostics composition

After Task45, use NEW short pinned copy from66group fixture, same16cuts/oldsidecars unchanged and no new backend targets; actual host/generic owner/real validators/UI row/localizer/timer entrypoint retained. Define exact extra five groups/assertions/cleanup before creation; diagnostics brushes only through actual WinUI controls if separately proven, fakeview highlighting records no native windows. Safety review before first app execution. Fullarchive/input checks/build0/0, own bounded asInvoker71PASS/COMPLETE/exit0/noFAIL/stdoutstderr/postcheck/processfinished/final3bytes and fresh committed security joint once. No deletion/real DB/network/shell/history/storage and no native15/16 closure by programmatic fakeview.

Task46 refinement before fixture editing: product36b9e23, NEW short cdpiui-diag-36b9e23-20261109, same16cuts and all prior66case sidecars byte-identical. Fourteenth new sidecar DiagnosticsHostCases.cs; main onecall/count71/root/pin/log only. Five groups: (1) actual closed generic owner/real UI rows samecollection/variables and cold/lazy constructor, real timer300ms nonrepeating and direct private Tick entrypoint Stop->Rebuild; no auto-timer delivery claim; (2) exact real validator/reference order, actual UI row Source/SeverityText/Description/localizer, counts/hostsender/unified trace/equality and refresh; Brush/native visual not evaluated; (3) live current editor-before-options, unique environment-presence-guarded variable marker added after ctor then renamed to force CFG004, attached resource lookup changes, same collections/no snapshot; (4) single-collection-listener and property-observer reentry preserving original outer results/count continuation; (5) controlled editor/collectionReset/counterObserver/highlight exceptions through private host method retain partial state, no logger/errorwrapper endpoint. No rows factory substitution.

Controlled DiagnosticsView implements only EditorText and RefreshEditorHighlighting, records calls/thread and optional owned hook, all other action endpoints throw; IsViewLoaded=false. Private Invoke helper unwraps reflection inner exception for owned synchronous fault assertions only. Every fixture detaches model/collection handlers, clears hooks and calls real CancelBackgroundWork to stop its timer; no help/test/command execution. No env writes or values printed; marker presence-only guard fails own app if unexpectedly set. Source/runtime limits constructor notifications/automatic timer/native/real IO remain explicit. Independent safety/assertion review after fresh allarchive/buildinput check and build before firstlaunch; same71/COMPLETE/process/postcheck/final3literalbytes gate and fresh exactpinjoint+committedsecurity.

Verified Task46: product36b9e23/1221roots1470fullarchive/16cuts/14bootstraps/build0/0 73.62s, independent prelaunchACCEPT. PID38944 14:07:50.0600669Z->14:07:52.4322586Z exit0/71PASS+COMPLETE/noFAIL/stdoutstderr/postcheck/final3literalbytes. Newjoint36+EE1791/1791 nofailed/skipped1m16s GUI0/0 65.51s. All source/runtime/native limits and rulings explicit, no productchange.

### Task 47: ConfigMakerUserControl resizable panel layout

Basecd13231, product36b9e23. Spec section «Следующий проход исходного God-control: resizable panel layout». New Helper/CreateConfigHelper/{ConfigMakerPanelLayoutController.cs,IConfigMakerPanelLayoutView.cs,ConfigMakerPanelLayoutView.cs}; modify existing control constructor and Layout partial only; new Presentation/ConfigMakerPanelLayoutTests.cs and additive two pure links. No new host partial/interface/UI mode or settings/backend changes.

Pure port: RootWidth/RootHeight; FilesVisible/CommandsVisible/BottomVisible; FilesMinWidth; FilesMaxWidth/FilesWidth get/set; FilesSizerWidth/CommandSizerWidth/BottomSizerHeight; EditorMaxWidth/EditorWidth/EditorMaxHeight set; EditorHeight get/set. Controller constructor store port; IsUpdating, Invalidate(), RememberEditorHeight(double), Update(). Adapter constructor six Func<FrameworkElement>, two Func<ColumnDefinition>, one Func<RowDefinition>, store only. Host private forward and exact capture/invalidate order retained. Public host flags not used as implicit interface implementations, recorded flags unchanged.

- [x] Write absent-owner RED tests with actual double math and pure port: cold ctor, all visibility/dimension branches, invalid roots/shortcircuit, files min/cap/clamp, initialized/current/saved bottom height, reset/reentry/repeated getter reads and partial exception/finally semantics. No product adapter executed in unit tests.
- [x] Extract exact algorithm and lazy real adapter; focus/full GREEN, GUI SDK8x64 0warnings/errors, compiled/source public contract0removedchanged, diffcheck and physical counts.
- [x] Independent fresh new-range review, one mandatory RED/GREEN fixpass only/minorsdeferred/rulings explicit; commit/status. No claim native resizing or actualcontrol construction.

Verified: owner+portabsentCS0246RED,39focused/full1618/1618 nofailed/skipped13s GUI0/0 49.86s, compiled452/0+7normalF66DB84B...; freshreviewACCEPT0Critical/Important, splitter-constant testminor deferredtoactual48distinct11/17/23. Control899->828/Layout233->158/main240->244/owner95/port22/adapter41. Sourceinventory451/517->585/0 atc0ed350; no actualUI/fullControl/nativeproofclaim.

### Task 48: Actual WinUI layout adapter mapping

After Task47 NEW short pinned copy from71group fixture, same16external cuts/oldsidecars unchanged plus fifteenth bootstrap five groups=>76. Define concrete standalone WinUI elements/scenarios and cleanup after inspecting actual adapter, before creation; preserve live numeric property access and real WinUI property validation, no full control/Window activation/desktop input/settings. Source review confirms constructor/XAML host wiring, programmatic adapter proof stays distinct from native15/16. Safety/assertion review and allarchive/input check/build before firstexecution; one own asInvoker76/COMPLETE/exit0/noFAIL/stdoutstderr/postcheck/processfinished/final3literalbytes; fresh exactproduct+committedsecurity joint. If standalone Arrange does not produce positive ActualWidth/Height, record actual scope and pursue source/managed proof without faking native UI state or claiming gate green.

Task48 refinement before fixture editing: same16cuts and all prior71sidecar sources unchanged, fifteenth new LayoutAdapterHostCases.cs, fivegroups=>76. Actual standalone Grid/FrameworkElement/ColumnDefinition/RowDefinition on owned WinUI UIthread, no full ConfigMakerUserControl/Window/activation/desktop input. All9callbacks record current target/thread and can perform owned controlled callback only; port/owner constructors store-only. Cases: (1) lazy adapter/owner zero callbacks and zero-sized actual root shortcircuit; (2) arrange standalone root, all real getter/setter mappings/visibility with distinct files11/command17/bottom23 lengths and distinct workspace-width/editor-height targets; (3) real controller visible files at1200 and2000 width yields max472/900, widths250/900 and editor582/732, checking cap/clamp and eachsplitter contribution; (4) saved/current/invalidated height, hide/show and smaller root with realpropertyvalues; (5) live replacement of callback-selected standalone element, nestedUpdate guard and owned getter exception beforewidth assignment preserving priorMaxWidth/finally/retry. Hooks record ratherthanassert insideportcallbacks; allownedobjects no externalIO.

Root helper sets own Grid Width/Height then Measure/Arrange/UpdateLayout and requires positive ActualWidth/Height before positivebranch assertions; if frameworkcannotarrangeunparentedroot, fail/preserveevidence, never fakeactualdimensions or labelnativegate green. Fakeport tests cover numeric behavior, actualruntime coversadapter/nativeproperties onstandaloneelements only; originalcontrolwiring/earlyXAML remain sourceproved. Runtime ownprocess lifetime ownsobjects; callbacks nulled in finally, ownerIsUpdatingfalse/threadfactschecked. Safetyreview after fullarchive/inputcheck/build; same76process/postcheck/threebytes plusfreshjoint. Native15/16 remainopen.

Verified Task48: productc0ed350/1224roots1474fullarchive/16cuts/15bootstraps/build0/0 110.37s; independent prelaunchACCEPT. PID41396 14:20:08.1726222Z->14:20:12.0142052Z exit0/76PASS+COMPLETE/noFAIL/stdoutstderr/postcheck/final3literalbytes. Real standalone Arrange positive; sameadapter/owner realUIproperties/callbacks/splitters covered, no fullControl/invalidDP/nativeclaims. Newjointc0+EE1830/1830 nofailed/skipped1m31s GUI0/0 111.25s.

### Task 49: App window close lifecycle coordinator

Base7dc341f, productc0ed350. Spec section «Следующий проход App: window close lifecycle». New Helper/Basic/{AppWindowCloseCoordinator.cs,IAppWindowCloseOperations.cs,AppWindowCloseOperations.cs}; modify existing App.xaml.cs constructor/field and App.Windows.cs handler only. New Presentation/AppWindowCloseCoordinatorTests.cs/additive two pure links. No migration/security/modal/position/window factory implementation changes or new host interface/partial.

Port<TWindow class>: Func<bool> CaptureExitEnqueue(TWindow); bool IsMigrationWindow(TWindow); SavePosition/Detach/ReleaseModal/ForgetOwner(TWindow); Warn(Exception), CompleteMigration(), bool IsStoreWorking(), Task CreatePrepareAsync(), ExitIfIdle(). Owner stores List<TWindow>/port, HandleClosed(TWindow). Adapter constructor ModalWindowCoordinator, Action<Window>detach, Func<Task>createPrepare, Actionexit. Captures dispatcher at original point then returns Low queue closure (no earlyenqueue); original backend policies preserved.

- [x] Absent-owner/port RED tests realList/fake managedoperations: exact allbranches/order/currentCount/captured migration/enqueue, missing/duplicate windows, save/logger/detach/modal/remove/complete/forget/post-dispatch faults and partial state, reentry during cleanup, unawaited returnedTask/pending gate with bounded worker cleanup. No WinUI/real IO/process execution.
- [x] Extract exact workflow/thin realadapter, hostguards/forward; focused/full GREEN, GUI SDK8x64 0warnings/errors, source/compiled publicretention, diffcheck/counts.
- [x] Independent fresh newrange review, mandatoryRED/GREENfixes only/minorsdeferred/rulings explicit; scopedcommit/status. No claim actualClosed event/backend/native15/16.

Verified: CS0246missingownerportRED,33focused/full1651/1651 nofailed/skipped15s GUI0/0 55.99s; compiled452/0+7 normal04117229...; freshreviewACCEPT0Critical/Important, one managedwindowidentitycoverage minor deferred. App754->702/Windows190->133/xaml172->177/coordinator61/adapter41/port21. Sourceinventory451/517->588/0 at73d92a6; native/backend/Closed event notclaimed.

### Task 50: Actual App close coordinator/adapter composition

AfterTask49 NEW short pinned copy from76group fixture, preserve oldcases/16cuts and define new adapter lowlevel targets precisely beforecreation. Actual App constructor/owner/adapter/currentList/dispatcher callback should remain; real logger/window-position/modal/Prepare/migration/ExitIfIdle effects inert. Nonactivated owned Window objects permitted only after safety review of exact source/manifest; no full user-window registration/closure/startup/desktop input or actualClosed-policy claim without direct evidence. Define fivegroups/cleanup and need for17th boundary, sourcecheckallarchive/buildinputs, independent safety review before firstlaunch,81/COMPLETE/exit0/noFAIL/stdoutstderr/postcheck/processfinished/threebytes, fresh committedsecurityjoint. No native15/16/deployment/security closure by programmatic fake endpoints.

Task50 refinement before fixture editing: NEW short pin copy after49 from76groups, same16oldcutfiles plus seventeenth AppWindowCloseOperations.cs only; existing App.xaml.cs cut extended in two places: detachcallback recorder before original Closed/SizeChanged removals, and ExitIfIdle body -> context recorder (original exitpolicy source/unit outsidethisgate). New WindowCloseHostCases.cs sixteenthbootstrap; main oneawait/count81/root/pin/log only; all old14case sidecars unchanged.

Adapter lowlevel substitutions: SavePosition/ReleaseModal(true)/Logger target/CompleteMigration/ForgetOwner/IsStoreWorking/CreatePrepare delegate endpoint -> guarded memoryrecorders; IsMigrationWindow wrapper default executes original Welcome predicate, optionalperownedwindowoverride for truebranch without Welcome ctor; CaptureExitEnqueue retains real window.DispatcherQueue and sourceLow argument but TryEnqueue target wrapper records window/priority, default calls actual queue with same priority/action and canreject viacontrolledflag. Detach callback/actualeventremovals retained, _exit callback stillboundtoApp.ExitIfIdle, _createPrepare passedtohookforidentitybutnotexecuted. No realposition/settings/modalWin32/migration/Prepare/logger/exitpolicy.

Fivegroups: (1) actual App constructor/owner/adapter/sameList/modal/delegate targets/noeagercalls plus private wrongsender guard; (2) two nonactivated owned basic Window objects created only here, actualdefaultpredicate false, cleanuporder/membership and real Low callback deliveredonUI; (3) reentrant A/B close with perWindowmigrationoverride and exact window-specific save/detach/release/forget traces, two queuecallbacks; (4) controlledqueue rejection synchronous Appforward and unawaited pending/faulted Preparetask with same actual callbacktarget recorded; (5) ownedSave+logger and Forget exceptions retain sourcepartialstates/queue boundary with observed tasks. No nativeClosed event/fullhandlerfalsebranch/handledargs/createPrepare callbackbody claims. Nonactivated windows never Register/Activate/Close; normal ownedprocess exit reclaims nativeobjects, programmatic internalownercallonly. No desktopinput.

Perfixture app.OpenWindows must initiallyempty; mutate/remove only the two known references, neverunknownwindows; observers/hooks defaultthrow withoutcontext, flags/threadfacts recorded. All pending gatesreleased and queuedexits awaitedwith3s bound/cleanup barrierbeforecontextclear, original list emptiness and nativeprocessExit checked. Assertions outsidehooks; no full nativecleanup/leakguarantee beyondownprocess. Source/build/allarchive exact17cuts/16bootstraps, prelaunch independent safety review beforefirstnewWindow/applicationexecution;81/COMPLETE/exit0/stdoutstderr/postsource/processfinished/external3bytes/freshjoint exactcommittedsecurity. Native15/16 remainopen.

Verified Task50: product73d92a6/1227roots1478fullarchive/17cuts/16bootstraps/build0/0 90.60s, independent prelaunchACCEPT. PID49108 14:37:40.6481697Z->14:37:43.9617754Z exit0/81PASS+COMPLETE/noFAIL/stdoutstderr/postcheck/final3literalbytes. Two ownnonactivatedWindow identities/actualqueue/sourceLow/callbackstub covered, no realClosed/backend/exitpolicyclaims. Newjoint73+EE1863/1863 nofailed/skipped1m28s GUI0/0 92.84s. Futurefreshcopies onlocalNTFSFspace, historicalCcopiespreserved.

### Task 51: BlockCheck2 site-list workflow controller

Basea1576c3, product73d92a6. New ViewModels/{BlockCheck2SiteListController.cs,IBlockCheck2SiteListBackend.cs,BlockCheck2SiteListBackend.cs}; modify existing Main constructor/field and SiteLists partial only; new Presentation/BlockCheck2SiteListControllerTests.cs + additive two pure links. Real SiteListElement comes from referenced AddOns. SiteListItem/Storage/preview/cleanup/options/Core/security implementations unchanged.

Controller<TItem class> constructor same Available/Selected ObservableCollection, backend, Func<string>currentGeneratedRoot, ActionselectionChanged. IsSiteListLoading/SiteListLoadError and counts; LoadAsync(), AddCustom(string filePath,string sourceName), UpdateCustomAsync(content,displayName,sourceName,token), Select(item),Remove(item),SetSelected(IEnumerable<TItem>). Backend LoadAvailableAsync=>Task<List<SiteListElement>>, IsLegacyGenerated(path), Create(displayName,sourceName,path,boolcustom,string?editableContent,stringroot), Normalize(content), CreateCustomPath(), WriteAsync(path,normalized,token),Text(key),GetFilePath/GetDisplayName/GetDomainCount/IsCustom(TItem),UpdateCustom(TItem,string). Factory receives explicit false/null for original noncustom calls, original true/normalized for custom. Host public declarations plain forwards, no implicit interface flags.

- [x] Absentowner+port RED tests with real ObservableCollections/SiteListElement and explicitrowbackend: cold ctor/counts, load guard/event/culture/order/currentdefinitions/legacy/case filters/rootgetter/retry/cache, prelude/catch/finally faults and partialreentry; custom path/dedup/no-add, normalized empty/token/current-afterawait/captureditem/removal/overlap/update errors; selection/null/self-source/distinct/notify/count overflow. No actual file/DB/Storage/Window/cleanup.
- [x] Exact extraction and thin productionbackend; focus/full GREEN, GUI SDK8x64 0warnings/errors, compiled/source retention/diffcheck/physicalcounts.
- [x] Independent fresh review newrange, mandatoryRED/GREENonly/minorsdeferred/rulings; scopedcommit/status. Actualclosedgeneric/model/host/backend/task/thread nextgate, no native15/16 claims.

Verified: initialtestsyntaxcorrectedbeforeeligibleRED; CS0246missingownerportRED,32focus/full1683/1683 nofailed/skipped16s GUI0/0 51.20s; compiled452/0+7 normalBD9ABF77...; freshreviewACCEPT0Critical/Important, Write-content/unbounded-unit-gates minorsdeferred, actual52strongchecksplanned. Host555->426/SiteLists215->82/main231->235/owner133/backend26/port24. Sourceinventory451/517->591/0 at214b4b3; Item/Storageunchanged.

### Task 52: Actual BlockCheck2 site-list composition on F

After51 NEW short fixture and joint roots under F:/cdpiui-verify (verified localNTFS/free54GB). Old C: copies/archives untouched, future new archives also F. New paths/folders fresh/no-reparse check, exactsourcepin/old81sidecars unchanged, same17cuts plus narrow new SiteListBackend lowlevel IO targets only. Real factory/Item/model counts/host notifications/current roots/normalization retained. Define fivegroups=>86 and seventeenthbootstrap beforecreation; no Storage implementation edits. Only reads of knownown three input files permitted through real noncustom row constructor; customWrite/path/load/legacy endpoints inert and content-backed rows avoid File.ReadLines. Same86/COMPLETE/exit0/stdoutstderr/postsource/processfinished/final3bytes; independent safety review beforefirstexecution; fresh currentpin+committedsecurity joint. ExactFroot replacements must cover forward/backslash/doubleescaped strings in prepare/DirectoriesManager/main/root guards; task code root staysC. No tempcleanup/deletion/realDB/history/network/userfile writes.

Task52 refinement before fixture editing: F:/cdpiui-verify/cdpiui-sites-PIN-20261109 and new source archive under F:/cdpiui-verify/archives; taskroot/logs/scripts remainC, no oldmoves/deletions. Same17cutfiles+newBlockCheck2SiteListBackend only (18), allold81sidecars unchanged; seventeenth bootstrap SiteListHostCases.cs, mainoneawait/count86/root/pin/log only. Newbackend IO substitutions LoadAvailable/IsLegacy/CreateCustomPath/Write only; retain real Normalize/Text/modelpropertyread/update/factory. Noncustomfactory reads only own existing actions-sites/actions-insert inputs or an explicit own missing marker (modelCountDomains catchesIO); legacy marker filteredbeforefactory. Custommemorycontent avoidsfile reads; allfakewrite paths own sites-custom-N-M.txt remain absent, no actualWrite/DB/delete.

Fivegroups: (1) realMain/closedgeneric/collections/backend/coldstate/lazyroot and no backendload/runner endpoints; (2) pendingload/guard/UIcontinuation and exact same mutable SiteListElement/sv-SE ordering/blank+legacy/zero-missing/caseexclude/realrowDomainCount/cache/root, separatefailure/retry asneeded; (3) actual AddCustom ownedfile/no-add, select/pathdedup/removeidentity/SetDistinct/selfclear and four hostnotification names/current counts/CanStart; (4) actual normalized raw multiline->exactWrite.Content/token plusrealcustomrow/count and lateRoot afterawait, existing captured row updatedafterremoval/no rootrefresh, nocancelrecheck whenfakewritecompletes; (5) two pendingcustomtasks completionorder/currentroot plus boundedloadfault/retry/hostsender/UI and no realrunners/history/cleanup. Allqueries useactualModels; readonlybygetter collections stayidentity, no privatefields/Storage fakecompilechange.

Every async task tracked, all awaits bounded3s, owned load/write gates released in finally and tasks awaitedbeforeunsub/contextclear. No SourceFixture.Start/Prepare/cleanup called; original settings fake remains memory and newmethods mustnotalterRunCalls/History/Cleanup counts. Markers absent before and externalafterentire86run; literal3inputbytesafterexit required. Field/root/readSourceWindow old81scope remainsdistinct. ParentFmkdir guardedresolved/no-reparse/localNTFScapacity; generatedcode substitutions cover '/', single/double/quad-backslash full oldabsolute fixturepath, not taskCroot. Verify staleCfixtureabsolute absent fromnew prepare/main/check/builder/runner and expectedFDirectoriesManager beforebuild/review. Safetyreview afterallarchive/buildinputs/build beforelaunch; actualSDKDLL/exe asInvoker/nonadmin andF no-reparse; exactnewFjointcurrentGUI+committedEE once. Native15/16stayopen.

Task52 prelaunch refinement from independent safety preflight, before first application execution: three Task.Run completion workers now tracked and bounded3s with TrySetResult; first original sidecar/build log and first DLL snapshot preserved, corrected sidecar rebuilt in existing fresh Fcopy with NEW log, archivebuilder never rerun. Legacy sentinel now existing actions-insert.txt (knownDomainCount1 if incorrectly admitted), backend LegacyArguments asserted; absent zero marker remains sites-absent-item.txt. This makes runtime legacy exclusion distinguishable, normalized write and owner flow unchanged; all18cuts/oldsidecars/sourcepin unchanged. External final3bytes and custom/missing markers stillrequired.

Verified Task52: product214b4b3/Ffixture1230roots1482fullarchive/18cuts/17bootstraps, firstbuild0/0 108.87s originalDLL+sidecarpreserved, correctedbuild0/0 47.71s, independent finalprelaunchACCEPT. PID34040 15:02:04.9067454Z->15:02:10.3901784Z exit0/86PASS+COMPLETE/noFAIL/stdoutstderr/postcheck/processfinished/externalfinal3literalbytes+markersPASS. NewFjoint214+EE1895/1895 nofailed/skipped1m26s GUI0/0 75.79s. UnitpolishdeferredbutruntimeexactWrite/boundedgatescovered; source-onlyemptyText/native/IO limits explicit.

### Task 53: Native Task15/16 gates on214b4b3

NEW F:/cdpiui-verify/cdpiui-native2-214b4b3-20261109, archiveF:/cdpiui-verify/archives/native2-214b4b3-source.tar. New NativeGates2Bootstrap.cs based on only corrected Frame/manual-load+check bodies from old ManualSmokeBootstrap; no old deadline helper/nativecopy rerun. Two tabs: actual ConfigMaker and actual Frame-hosted ResultPage. No Main/progress/codec/window-reuse helper scopes. UI buttons for check loaded/readonly, real saveDialog, inert export, real New confirmation, load/check manual and Finish. Sticky flags11PASS+COMPLETE; known own Window.Closed exits process, asInvoker/no elevation; same own dataRoot/explicitprocesspath guard. All dialogs actual except filepickers; actual Add/Move only throughSky.

Planned19boundaryfiles: Program,App.xaml,DirectoriesManager,ComponentCommandHelpService,ConfigMakerViewModel main (fake manager/installed predicate/editor settings/handlerfailuretrap),ConfigMakerUserControl main (output2targets),SearchUserControl(read2preferences/refuse2writes),ConfigMakerPresetResources(component/base-dir DBtargets),ConfigMakerPresetStorageService(private SaveCore only),ConfigMakerPresetExporter,ConfigMakerFileDialogs(3pickers inert),BlockCheck2ViewModel(main optionssettings),BlockCheck2SiteListBackend(load/path/write inert),BlockCheck2MainPageViewModel(refuse scan),BlockCheck2ResultPageViewModel.ManualTesting(refuse Start),BlockCheck2ResultPageViewModel(real strategytesttargetrefuse),ConfigMakerPresetFileActionsBackend(preferences/shelltargets inert). Additional ConsoleFontHelper6preferences memory(defaulttrue/Consolas/16), Core.SettingsManager ctorbaseargumentRefuseNativeSettings throws beforeXMLbase andsetsstickyAppContextswitch; require switchfalse atFinish AND afterApplication.Start returns(beforeexitcode). Actual controlXAML/layout/diagnostics/doccoordinator/realmodel/codec/manualeditor/FrameMenu remainpin exact. No App.WindowClose backend changes because windowunregistered/customeventonly. One bootstrap +verifiedemptySecret asonlyC#extras. Allrepo/sourceinputs compared to214, expectedpatchset exactly19; no native app start until independent fresh safety/assertion approval+hashes.

- [x] Design/source/build/check full pinned new F fixture and review guard/ownedinput/log/timeout/manifest and all native paths before launch.
- [x] Launch exactasInvoker ownexe; use freshSky target snapshots/inputs for all11observations; record screenshot/AX/log evidence, no coordinate guessing/shortcut permissions.
- [x] RequireCOMPLETE/noFAIL/exit0/stdoutstderrempty/processfinished/postsource/inputbytes/noexport; markTask15/16nativeboxes only for actualobserved phases. Preserve alloldcopies and scope gaps (productionbackend/startup stillunproved).

First native17cut copy preserved: PID52048/11PASS+COMPLETE/exit0 but mandatorypostSettingsabsence FAIL (ConsoleFontHelper x:Bind initialized Settings.xml/bak beforeUI). Overallgate notgreen; newnative2only after19cutreview, repeat11actions. No oldcopyrerun.

Verified final native2: exact214/19cuts/1230roots1482fullrepo/build0warnings/errors75.21s/independentprelaunchAPPROVED; GUI86EC3A27…/EXEE17175E…/CoreE18B0918…/Shared15CCF56E…/AddOns05038F2A…. PID36508,15:44:45.2758468Z→15:47:31.5834895Z,11PASS+COMPLETE/exit0/noFAIL/stdoutstderr/postsourcecheck/processfinished/Settingsabsent/only2allowednewbinfiles/literal23inputbytesretainedSHA70CE5088… PASS. ActualSkyobservationsseven document cases/four manual phases, own AX retained. Native15/16boundedgatescomplete; productionbackend/startup/actualexport/autocorrect/settings/fullwindowlifecycle notclaimed. First17cutrunfailure remainsFAIL.

### Task 54: Final requirement and evidence map

After53, refresh selectedaudit scope/owner map/physicalcounts/pins and exactchecks; historical first-window acceptance unchanged. Inspect root/status/source/normalDLL preservation/latestjointpin/securityownership/no uncommittedforeignchanges/no activeownedprocess. Independent final integration/requirements review per executing-plans skill, with closedcodec body review not reopened without new integration evidence; mandatoryfindingsoneRED/GREENpass, minor/rulingsreported. No redundantgreenruns fordocs-only, no force/reset/delete/publish/merge. Current branch remainslocal/reviewable, all remaining native/fullbackend/wholeaudit limits explicit.

Verified Task54: freshwholebranchACCEPT f39e8a0a→1d76cbdb,153paths/68commits/all4clusters/96validGUIlinks/30changedpresentationtestfiles;0CriticalImportant,3Minor deferred afterrootbaselinecheck. Every13declinedjudgment ledgeredwithcost, exhaustiveappendixupdated. No mandatorycodefix/rereview/redundantgreenrunsforfinaldocsonly; product214/root1683GUI0/0/source451/compiled452/0+7/joint1895/native2eleven+allpostchecks remainexact. Keepbranch/worktree/evidence peruserauthorization; no integrationmenu needed foralreadyselected safe local-preservation scope.

### Task 55: ConfigMaker preset structure state and publication

Base28ceef44c0e438885ecb229a70c41cc1d9aeaa91, previousproduct214b4b3. Extract new Helper/CreateConfigHelper/ConfigMakerPresetStructure.cs, generic internal sealed ConfigMakerPresetStructure<TNode>: ObservableObject. Existing PresetStructure partial delegates; existing private node setters retained. No new host interface/partial/XAML/backend/schema/settings changes. CurrentTreeBuilder/Adapter remain real and supplied through closures; owner takes six live funcs (command, zapret2, extractFiles(command), attachedFiles, projectFiles(files), projectGroups(options,explicitGroups,zapret2)). Constructor stores only. Owner owns explicit arrays/use flag/two node lists/two flags, same public property names, Set/Clear/Rebuild and AvailabilityChanged. Host forwards owner PropertyChanged with host sender and availability with EventArgs.Empty.

Exact temporal contract: guards files then groups beforeenumeration; file filter nonblank/dedup kind+OrdinalIgnoreCase beforeToArray, assign files beforegroup enumeration, set explicitflag afterboth arrays. Rebuild extract currentcommand ->ToArray, enrich explicit missingoption only first matchingkind/path, attachedToArray, basefirstconcat/dedup; fileprojection then fieldassign+propertyevent BEFORE reading currentcommand/groupstate/zapret2 for group projection. Compute availability aftergroupbuild against current fields, assignbothflags BEFORE groupnodepropertyevent, then unconditional3derivedevents, availabilityevent onlybooldelta. No reentryguard/atomic snapshot/rollback. Clear resets explicitflag/arrays then Rebuild. Cold HasPresetGroups/IsVisible true; actual projection allocates empty collections as before.

- [x] Write realmodel/Core/parser/comparer + genericmanagedprojection tests: defaults/noeagercalls, explicit filtering/firstdedup/enrichment/precedence/attached live, option/source coords and livecomponent/command reads, collectionidentity/equality/eventsequence/flagtiming, Set guards/partial enumeration, allcallback/eventthrow partialstates, Clear/reentry/nested publication/currentfield timing; no filesystem/localizer/WinUI fakes masquerading as actual controls. Observe eligible absent-owner RED before source.
- [x] Extract exact state/algorithm and wire production closures. Focus/full GREEN +GUIx64 0warnings/errors, source+compiled recorded API including retainedprivate node setters, physicalcounts/diffcheck; scopedcommit. New range stillsubjecttofinalreview58.

### Task 56: Actual preset structure composition

New pinnedFfixture from previous86group currentownerhost harness: same18external boundary files/old17bootstrap sources unchanged, new eighteenth PresetStructureHostCases (five groups =>91). Real host/genericowner/resources/builder/WinUIadapter/nodecontent/current delegates/eventsender/UIthread; no extra backend effects. Cases: lazy/default/actualowner wiring; detected/explicit/enrichment/attached realmodels/treecontent; exact empty/nonempty hostevents/availabilityflags/newemptyarray/private-set compatibility; hostobserver command/component/explicitstate reentry and currentgroup reads; controlled projection/read fault partialstate+retry. All callbacks/hooks restored, no unboundedTasks/no new file IO/nativeWindow activation/cleanup. Sourcecheckallarchive/buildinputs/newpin+exact18cuts/18bootstraps, independent prelaunch safety approval before app.91PASS/COMPLETE/exit0/noFAIL/stdoutstderr/postsource/3literalinputbytes/processfinished. New source/publiccontract and exact committedsecurity joint forcurrentpin. Old copies/logs preserved.

### Task 57: Current native document/manual regression gate

Because55 changes realcontrol tree publication, repeat exactly the approved11native observations in a NEW currentpinFcopy derivedfromnative2. Firstnative3 failed afterunsolicitedUIsequenceandispreserved; acceptedfreshnative4 usesexclusivecross-chatUIqueue. Same19explicitcuts, pureSettingsguard+Finish/postApplication.Start probes, oldnative2copyneverrerun; allrealXAML/realcontrols/owners remainpinexact. Same independentfreshprelaunch safety, allarchive/buildinputs/hashpins/ownpath/asInvoker/nonadmin/freshoutputs/no reparse. Only named11Sky actions, actualSave/Newdialogs plusAdd/Move; all11+COMPLETE/exit0/noFAIL/stdoutstderr/postsource/Settingsabsent/only2newbinfiles/literalinputbytes/processfinished required. No fullstartup/actualbackend/resize/otherbuttons claims.

### Task 58: Continued-batch review and close

Fresh-context final review of the entire continuation28ceef4..current with whole-branch integration context, priorclosed1..54 review notblindlyrepeated. All original5ReviewFocus items andnewstate/publication/reentry/adapter ordering checked deliberately. Regrade byusereffect; mandatoryONE RED/GREENfixpass+fullchecks, Minor deferred/rulings foreachdeclinedline. Refresh map/status/decisions/currentpins; local scopedcommits, no deletes/reset/force/push/merge/publish. User explicitly resumed; previous completion describespriorcheckpoint only.

Task55 refinement fromGUI RED: existing PresetFiles explicit IConfigMakerPresetFileReplacementHost.UsesExplicitPresetStructure/ExplicitPresetFiles forward to rawownerproperties, preservingarrayidentity/no extraevents/rebuild. TwoextraCS1061RED tests beforefix. Task56 one copied ReplacementHostCases helper uses that existingport instead ofremovedprivatefield; preserveoriginalsource andallother15casefilesraw, record exactone-helperdelta inchecker/review.


Verified Task55/56: productd347c617, test-only3ba7d388. 36focused/1719full/GUI0warnings-errors65.78s,
source451/517→592/0comparedchanges, compiled452/0recordedchanges/7retainedinterfaceadds; normal0789930.
Actualstruct2 currentpin exact18cuts/18bootstraps/1231project1486repo, build0/0 and independentprelaunchACCEPT;
PID35892 91PASS+COMPLETE/exit0/noFAIL/empty streams/postsource/threeexactretainedinputs/processfinished.
Initialstructfixturebadtestsetup caughtbeforeexecution, preservedunlaunched. Jointd347+EE1931/1931GUI0/0.
Continuationreview0Critical/0Important/1Minormanagedflag-observability; test3ba7d388 strengthensexplicitmode,
36/1719repassed; sandbox12AddOnsIO failuresdiagnosedUnauthorizedAccess thenunrestricted1719GREEN.
Task57 currentnative4 eleven observations and allpostchecks complete; Task58 finaldocumentation/review pending. Native2 retains oldpin.


Task57 verified on NEWnative4 after native3 unsolicitedsequenceFAIL/exit1 retained. Same19cuts/d347/XAML,
build0/0 70.03s/sevenartifact+manifest pins/independentprelaunchAPPROVED. Explicituserauthorizedqueue:
CDPIUI peer+lessons pausedALLUIAPI, activeRootcheckedbeforeeachSkycall, no concurrentUIreads/input.
PID50036 21:32:14.1121727Z→21:48:15.6098961Z,11PASS+COMPLETE/exit0/noFAIL/empty streams/postsource1231+
1486/exactmanifest/Settings-export-customabsent/only2newbinfiles/exact23inputbytesSHA70CE5088/processfinished.
AfterlastfreshUIinventory RootreleasedALLUIAPI toCDPInativefilesowner; no furtherGUIinthischatwithoutlease.
Native3 neverrerun, inputsourceunknown; minimized/no-monitor captures recoveredfreshstatewithnorepeatedAdd.

Task56 verified-scope qualification: actual host callback changes command and explicit groups during file
publication; component is set Zapret2 beforehand. Mid-publication component flip is managed+production
live-closure source proof, not actual-host runtime proof. Direct projection fault remains managed/source-only
as previously ruled. Additional live component-switch case deferred explicitly, no product defect claimed.

Task58 complete: fresh final review28ceef44→3ba7d388+fourdocs ACCEPT0Critical/0Important/1Minor(scope
qualification above). No mandatoryproductfix; existing1719/GUI0-0/451+452API/actual91/joint1931/native4
eleven+allpostchecks retained exactpins; no redundantgreenrunsfortext. Decisionsappendix111Ruling17Minor.

### Continuation59–61: App idle exit

Spec: [App idle exit](../specs/2026-11-10-app-idle-exit-design.md). Execute inline per executing-plans;
user explicitly selected autonomous continuation, so no interactive plan handoff. Base2400dee.
Global constraints above remain; no settings/backend/migration/startup/security changes or UI use without lease.

Review Focus for this continuation (also retain original five): (1) live Count/migration short circuit;
(2) close changes state before idle decision; (3) warning/fallback exceptions keep their original boundary;
(4) unawaited/pending/faulted creation and nested events; (5) real App-bound callbacks and lazy host wiring.

### Task 59: App idle exit controller and characterization

Files: create `CDPIUI/Helper/Basic/AppIdleExitController.cs`,
`CDPIUI.Tests/Presentation/AppIdleExitControllerTests.cs`; modify `CDPIUI/App.xaml.cs` and test csproj.
Produces internal constructor `(Func<int> windowCount, Func<bool> migrationPending, Func<bool> storeWorking,
Func<bool> goodCheckRunning, Func<Task> createPrepare, Action closePrepare, Action exit, Action<Exception> warn)`;
`void HandleStoreStateChanged(bool isWorking)` and `void ExitIfIdle()`. Host private signatures unchanged.

- [x] Write literal call-trace tests for all four exit guards, repeated live count, working guard/Create,
  stopped close-before-current-read, each throwing port, warn/fallback throws, reentry and ignored pending/faulted Task.
  Constructor calls nothing; each test states the behavior mutation it detects. Observe absent-owner CS0246 RED.
- [x] Extract unchanged policy and wire eight lazy production ports. Host warning keeps original category/message;
  close coordinator retains host method group. Focus/full GREEN, normal GUI x64 Release0warnings/errors,
  source451/compiled452 recorded shapes, physical counts, diffcheck; commit only owned paths.

### Task 60: Actual App idle composition gate

Create new pinned F verification copy from struct2, preserve old91groups. Use existing external-cut App.xaml
to provide controlled create/close/exit/Store/GoodCheck endpoints while exercising real private App forwards,
real owner, live App.OpenWindows and real migration flag. Inactive new context preserves old close-group
Exit recorder. No activated PrepareWindow, full startup, backend or persistent settings. Add grouped assertions
for live window-list replacement/migration, Store state/close current reads, throw/fallback/reentry and callback
identity. Optional actual component-flip scope extension only if independently specified and reviewed.

- [x] Record exact source cuts/inputs/bootstrap/artifact pins; independent prelaunch safety review.
- [x] Run bounded nonactivated programmatic host only after peer coordination; all expected PASS/COMPLETE,
  exit0/noFAIL/empty streams/source/input/absence/process checks. Refresh exact committed security joint.

### Task 61: Integration review and checkpoint

- [x] Fresh-context review2400dee..current plus docs with original five and continuation five Review Focus.
  Mandatory defects get one RED/GREEN fix pass and appropriate checks; Minor explicitly adjudicated.
- [x] Refresh status/map/decisions pins/scopes; scoped local commits. Preserve all evidence/worktrees and
  continue the user-authorized project work; no reset/delete/force/push/merge/publish.


Task59 verified839cc952:39focused/1758full/normalGUI0-0/451source593files/452compiled0+7,
App702→677physical lines. Independent product review2400dee→839cc952 ACCEPT0CriticalImportantMinor.
Joint exact839+ee477161970/1970 andGUI0-0. Task60 newFidlefixture built0-0/71.39s,
1232project1489repo/exact18boundaryfiles19bootstraps, old91+6App=97, prelaunchREADY0/0/0.
Runner-only threeMinors fixed/read-onlyrechecked; all5binaries+3sourcepins unchanged.
Runtime97 NOT RUN: sharedownerV3peer-nativefiles remainsactive/waitingOnApproval; explicitrelease
notreceived, lessonsnextready/allUIpaused. Root allUIpaused/no newWinUIprocess, nolease stealing or
foreignapprovalconfirmation. Task60runtime andTask61finalclose remain OPEN untilexternalqueuehandoff.
LastclosedruntimeD347/native4 eleven+allpostchecks remainsacceptedwithownpin. Product review isaccepted,
not an assertionofruntimecompletion. Preserveone-shotfixtureandreviewedrunnerSHA9AF91AFE… forresume.


Task60 COMPLETE afterexplicitpeerhandoffV4Root/lessonswithdrawn:97PASS+COMPLETE/exit0/noFAIL/empty
streams, PID61180 2026-11-10T05:09:45.0171515Z→05:09:49.2267677Z,1232project1489repo/exact18cuts19
bootstraps/sourcepins/threeexactinputs/settings-export-markersabsent/only4newbinfiles/priornames/processfinished.
Reviewedrunner9AF91AFE unchanged. NoRootUIAPI; afterpostchecksRootallUIpaused andqueueV5handedpeer.
Product839/normal0DD unchanged, prior39/1758/GUI/API/joint1970 preserved; finalruntime/docsreview61next.

Task61 COMPLETE finalruntime/evidencereview0Critical0Important1Minordocwordingfixed:threeinputfiles
16/16/15bytes,notthreebytes. No productfix/rerunrequired, all97uniquePASS/first91sameordertext/hashpins
confirmed. Prior115Ruling17Minorhistoricalappendixretained. Continue userauthorizeddecompositionnext.

### Continuation62–64: BlockCheck2 result selection

Spec: [result selection](../specs/2026-11-10-result-selection-design.md). Base855dbd3. Inline executing-plans,
preserve original global constraints and autonomous execution. Review Focus5: captured item despite menu
reentry; live running/manual reads between notifications; list predicate before running guard; localization/
formatting faults leave exact partial state; host sender/private setters/two-way nullable check retained.

### Task62: Selection state and publication owner

Create `CDPIUI/ViewModels/BlockCheck2/BlockCheck2ResultSelection.cs` and
`CDPIUI.Tests/Presentation/BlockCheck2ResultSelectionTests.cs`; modify existing PageViewModel.cs/testcsproj.
Constructor consumes Func<BlockCheck2StrategyEvidenceItem?>, three Func<bool> (session/running/manual),
Func<BlockCheck2StrategyEvidenceItem,bool> list predicate, Func<string,string> text, Action menu.
Produces15same-named state properties with get/set, defaults preserved, and `void Update()`.

- [x] Write literal branch/state/event/port-order tests, reentry/captured item/live flags, repeated equality,
  nullable setter, localizer/format/menu/observer failures. Observe absent-owner CS0246 RED before owner.
- [x] Move exact algorithm/state, preserve host private/public setters and sender. Focus/full GREEN, normal
  GUIx64 0warnings/errors, source451/compiled452 recorded-shape checks, physicalcounts/diffcheck; scopedcommit.

### Task63: Actual selection composition

- [x] Fresh F copy of newpin from idle97: same18boundary files/old19bootstrap sources, add actual Page
  selection cases using current real Result/HasSession/evidence/localizer/owner/private-set/public nullable
  ports. Fakeview onlySelectedStrategy/NotifyMenu; other methods throw, no lifecycle/backend endpoints.
  Literal details, flags, hostsender/events, menu reentry/current Result/running and partial faults. Specify
  group count and exact hashes before launch; independent read-only prelaunch review, allinputs/cuts.
- [x] Under new explicit Root lease run expectedPASS/COMPLETE/exit0/noFAIL/empty streams/source/pins/input/
  absent outputs/prior names/owned process gates; new exact committed-security joint and normalDLL unchanged.

### Task64: Final continuation review

- [x] Fresh review855dbd3..current with originalfive/newfive Review Focus; adjudicate every finding,
  mandatory one RED/GREEN fix pass; Minor explicitly ledgered. Refresh map/status/decisions/scopedcommits.
  No publication/merge/reset/delete or another session's work. Continue after coherent checked checkpoint.


Task62/63verified: productde3/normal9EBC,28/1786GUI0-0/source451(594)compiled4520+7/joint1998GUI0-0.
Productreview0CriticalImportant1Minortestcoveragefixed ffa65fc9: controlled-cachemutantRED, restoredsource
29focused/1787fullGREEN, no productchange/GUIrerun. Firstselectioncopyunlaunchedretained; freshselection2
negativecurrentResult/listcase+throwingmenuorder strengthened, build0-0/55.58s/prelaunch0-0-0.
V6Rootlease+readytrue, PID45692 UTC2026-11-10T05:39:53.4500889Z→05:39:57.3058215Z,103PASS/COMPLETE/
exit0/emptystreams/source1233project1492repo/18cuts20bootstraps/sourcepins/inputfiles16-16-15bytes/
Settings-export-markersabsent/4newbinfiles/priornames/processfinished/5actualbinarySHAprelaunch+postexitPASS.
RootnoUIAPI, RootallUIpaused/V7releasedafterprocessabsence; Task64finalreviewnext. No productchangesafterde3.

Task64 COMPLETE finalfreshreview0Critical0Important0Minor aftertest/doc refinements. All103unique,
first97sameordertext, retainedlogsbyteequal,5binary+4sourcehashespre/post/currentmatch,inputfiles16-16-15,
522→526files4newnames/no forbiddenoutputs/PIDgone/V7released. Product unchanged, source/API/UIevidence
retainsDE3pin,test-onlyFFAretest29/1787. Historical118Ruling17DeferredMinor preserved; docs-onlyclose.

### Continuation65–67: report summary presentation

Spec: [report summary](../specs/2026-11-10-report-summary-design.md), base57ede2c. Inline, originalglobal
constraints retained. Review Focus: live result reads between localization calls; precedence NoBypass vs
Success/BestEffort; HTTP-only short circuit; exception before Show produces no notice; real private host
forward/current Result + host-bound delegates. Originalfive paths remain integration-review inputs.

### Task65: Report summary presenter

Create `CDPIUI/ViewModels/BlockCheck2/BlockCheck2ReportSummaryPresenter.cs`,
`CDPIUI.Tests/Presentation/BlockCheck2ReportSummaryPresenterTests.cs`; modify PageViewModel.cs/testcsproj.
Consumes five Func<bool>, Func<string,string>, Action<BlockCheck2NoticeSeverity,string,string> in exact
HasReport/NoBypass/Success/BestEffort/HttpOnly/text/show order. Produces `void Show()`; ctor stores only.

- [x] Literal key/severity/trace/precedence/short-circuit/reentry/fault cases; absent-owner RED.
- [x] Extract unchanged algorithm, lazy current Result ports/private forward; focused/full GREEN,
  normalGUI/API/physical counts/diffcheck, scopedcommit. No lifecycle/backend changes.

### Task66: Actual summary composition

- [x] New pinned F copy from accepted103, preserve20oldbootstraps/18cuts, add4realPage/Result/report/
  localizer/private-forward/inertShowStatus groups=107. Empty/success/best/partial/NoBypass/http-only,
  current Result replacement, reentry/error boundary; other view calls throw. No UpdateView/lifecycle/native.
- [x] Independentprelaunch exactsource/artifacts, explicitRootlease,107PASS/COMPLETE/exit0/allpostchecks
  incl5actualbinarySHApre/post. New committedsecurityjoint/normalbinarypreservation; release queue.

### Task67: Final review and checkpoint

- [x] Fresh review57ede2c..current with originalfive/newfive focus, mandatoryoneRED/GREENpass,
  explicitminor/ruling adjudication; refresh reports/pins/scope/scoped local commits. Preserve evidence.


Task65/66verified:930product/normalC4DC,20focused/1807full/GUI0-0/API451(595)4520+7/joint2019/GUI0-0.
Productreview0CriticalImportant1Minortestcoveragefixed4d19d461: titlecallbackchangesonlyBestEffort,
cachedBestmutantRED→originalsource21focused/1808fullGREEN, sourceBYTESrestored/noGUIchange.
107PASS+COMPLETE/exit0/PID45484 UTC2026-11-10T06:00:48.7342523Z→06:00:52.8956430Z/noFAIL/empty
streams/source1234project1495repo/exact18cuts21bootstraps/5sourcepins/3inputs16-16-15/forbiddenabsences/
4newbinfiles/priornames/PIDgone/5actualbinarySHAprelaunch-postexit. V8Rootafterexplicitreleasedqueue+all
paused+readiesfalse+prelaunchREADY/ownreadytrue; after107V9releasedallUIpaused. Finalreview67next.

Task67 COMPLETE finalreviewACCEPT0Critical0Important0Minor aftertest-onlyBestEffortreentrycase+headerdoc
correction. Actual107unique/first103sameordertext/logsbyteequal/5binary5sourceSHA/exact3files16-16-15/
empty streams/exit0/noFAIL/PIDgone/V9released independentlychecked;20/1807original930+test-only21/1808
andjoint2019/GUI0-0/source451(595)compiled4520+7 distinctpinsretained. No newproductfix or redundantGUI/
native rerunfordocs. Historical120Ruling17DeferredMinor preserved, localreviewablecheckpoint.

### Continuation68–70: selected strategy test

Spec: [selected strategy test](../specs/2026-11-10-selected-strategy-test-design.md), basee1bcdf8.
Inline executing-plans; user explicitly authorizes autonomous selection/execution without confirmations.
Original global constraints/five review inputs remain. New Review Focus: pre-try failures leave flags;
captured item but current Result after notifications; pending/reentry guard; catch/finally exception
precedence; first issue/Error, formatting before Error, zero attempts and culture-dependent display.

### Task 68: Selected strategy test controller and characterization

Files: create `CDPIUI/ViewModels/BlockCheck2/BlockCheck2SelectedStrategyTest.cs` and
`CDPIUI.Tests/Presentation/BlockCheck2SelectedStrategyTestTests.cs`; modify PageViewModel.cs/testcsproj.
Constructor consumes Func<evidence?> selected, Func<bool> running, Action<bool> setRunning,
Action<bool> progress, Action selection, Func<evidence,Task<manualResult>> test, Action assignments,
Func<string,string> text, Func<issue,string> issueText, Action<string> error,
Action<severity,string,string> show. Produces Task TestAsync(), void ShowResult(manualResult).

- [x] Write guard/literal traces/pending/captured item/reentry/fault-boundary and presentation tests
  specified above; run focused filter and observe absent-owner CS0246 RED.
- [x] Extract unchanged algorithms and lazy host wiring; focused/full GREEN, GUIx64 0warnings/errors,
  source451/compiled452 recorded shapes, physical counts/diffcheck; scoped local checkpoint.

### Task 69: Actual selected test composition

- [x] New immutable pinned F copy from accepted107, preserve21old bootstrap sources/18boundary files;
  add4groups=111 using real Page/Result/localizer/private forward and narrow inert view. One inert model
  session is loaded then replaced with a sessionless model; no external component/backend operations or
  new source cuts. Independent prelaunch review of exact inputs/pins.
- [x] Explicit coordinated Root UI lease,111uniquePASS+COMPLETE/exit0/noFAIL/empty streams/source+binary
  pre/post pins/retained inputs/forbidden absences/prior names/process finished; release queue.
  Exact committed-security joint full tests/GUI and normal GUI binary preservation.

### Task 70: Final review and checkpoint

- [x] Fresh review e1bcdf8..current plus original whole-branch integration context, original/newfive focus.
  Mandatory one RED/GREEN defect fix pass, explicit Minor/rulings. Refresh status/map/decisions/pins,
  scoped local commits. Preserve evidence and other sessions' ownership; no publication/deletion.

Task68/69 verified: productff6bc636/36focused1844full/GUI0-0/54.40s/source451(596)compiled4520+7.
Test-onlydfaccbd9 closes2coverageMinors: trueCanceledTask and liveIssues replacement during format;
cache-firstError mutantRED→restoredsource38focused74ms/1846full13sGREEN/productreview0/0/0.
Actual111unique/first107sameordertext/COMPLETE/exit0/PID9112gone/empty streams/logsbyteequal/allpostchecks,
UTC2026-11-10T15:06:27.9269258Z→15:06:37.3261527Z, build0-0/78.84s/prelaunchREADY,
1235project1498repo/exact18cuts22bootstraps/5binary6sourcepins/3inputfiles16-16-15/forbiddenabsences/
4newbinfiles/priornames. ExplicitpipelineV15handoff→RootV16→V17released/RootallUIpaused; pipeline
notified to resume. Joint exactdfaccbd9+EE2058/2058/GUI0-0/92.08s. Normal96564963 unchanged.
Task70 COMPLETE finalreview0Critical0Important0Minor; no product changes afterff6, historical122Ruling17Minor retained.
FinaldocMinor corrected immediately below historicalruling122: actual111ff6 vsjointdf+EE2058. No productfix
or redundanttest/GUI/native rerun; evidence/5binary6sourceSHA/111uniquefirst107/logs/input/PID independentlyverified.

### Continuation71–73: report export

Spec: [report export](../specs/2026-11-10-report-export-design.md), baseff9db65. Inline executing-plans,
autonomous user authorization and original constraints retained. Review Focus: live current report after
picker/localization; setup faults outside catch; success-localizer/status faults inside catch; independent
concurrent paths/tasks; exact real serializer binding and report IO restricted to two fresh own files.

### Task 71: Report export controller and characterization

Files: create `CDPIUI/ViewModels/BlockCheck2/BlockCheck2ReportExport.cs`,
`CDPIUI.Tests/Presentation/BlockCheck2ReportExportTests.cs`; modify PageViewModel.cs/.Export.cs/testcsproj.
Consumes Func<report?>, Func<string,string,string,string?> picker, two Func<string,report,Task> save ports,
Func<string,string> text, Action<severity,string,string> show, Action<string> error. Produces
Task SaveJsonAsync(), Task SaveTextAsync(), Task SaveAsync(Func<Task>); no ownership of files/components.

- [x] Literal branch/trace/live-report/pending/concurrent/reentry/cancellation/fault tests before owner,
  absent-owner CS0246 RED; then original algorithm extraction and lazy host wiring, focused/full GREEN.
- [x] Normal GUIx64 Release0-0/source451/compiled452 recorded shapes/current physical counts/diffcheck;
  scoped local commit with exact immutable source/API pins. No clipboard or serializer changes.

### Task 72: Actual report export composition

- [x] Before fix, isolated expected-RED on bd91343 proves eager null-view picker binding with real host:
  old111 PASS then named constructor FAIL/exit1, no new report IO. Preserve unlaunched first115 and
  compile-failed red1; fresh red2 only repairs test catch-name collision. After RED lazy lambda fix,
  same null-view constructor case must pass in a new immutable GREEN115 copy.
- [x] Fresh pinned F copy from111, preserve22oldbootstraps/18cuts, add4groups=115. Real Page/Result/report/
  Localizer/serializer/private save helper, inert picker/status only. Two direct-child report outputs
  json/text written/read back/retained, third marker must remain absent; exact6newbinnames, no other IO.
  Independent prelaunch READY covering scope change and full pins/archives/nonadmin/freshness.
- [x] Coordinate explicit UI handoff/grant;115unique/first111/COMPLETE/exit0/empty streams/allsource+binary
  pre/post pins/input+output contents/forbiddenabsences/priornames/owned process gone; release queue.
  Exact committed-security joint full tests/GUI; preserve normal binary and other sessions' work.

### Task 73: Final integration review

- [x] Fresh review ff9db65..current with original+new five focus and previous closed branch context;
  mandatory RED/GREEN defect fix pass, explicit rulings/Minor, accurate API/runtime/IO limits.
  Refresh docs/pins/local commits; no destructive actions/publication or evidence cleanup.

Task71/72 verified: initialbd91343 31/1877/GUI0-0/API597/4520+7; productreview2Minor fixed viaactual
RED2bd111oldPASS+namedCtorFAIL/innerNREline55/exit1/noCOMPLETE (PID53604/allpostchecks), thenlazy
pickerfix0c5ea124. Tasktiming62e source mutation4JSONFAIL/4textPASS→restore→31/1877GREEN included.
Current0c5 31focused40ms/1877full14s/GUI0-0/18.46s/source451(597)compiled4520+7/normal0A96,
joint exact0c5+EE2089/47s/GUI0-0/69.41s. Original115bdunlaunched/red1compilefailedunlaunched retained.
GREEN115 new0c5 copy/69.96sbuild/prelaunchREADY, sameB7AEBFcase asRED; PID71744 UTC15:52:46.0727531→
15:52:50.1694270/exit0/115unique-first111/COMPLETE/logsbyteequal/empty streams/noFAIL/1236project1501repo/
18cuts23bootstraps/5binary7sourcepins/3inputs16-16-15/2realoutputs354-260bytes/currentfields+SHA/
6newnames522→528/priornames/negativepathSettingsmarkersabsent/PIDgone. V23pipelinehandoff→24Root→
25released/RootALLUIpaused+ack/requestfalse; pipeline notified, no moreRootUIinpackage.
Task73 COMPLETE finalruntime/docsreviewACCEPT0Critical0Important0Minor;124Ruling17DeferredMinor retained,
no newproductsourceafter0c5. Independentfull12pins/3inputs2outputs/115uniquefirst111/logs/528names/
absences/PID/REDvsGREEN/samecase/jointexactpins/normal0A96 checked; no rerunforfinaldocs.

### Continuation74–76: manual component test session

Spec: [manual test session](../specs/2026-11-10-manual-test-session-design.md), base0d8ba6f. Inline
executing-plans, autonomous authorization and original global constraints. Review Focus: live repeated ID
vs single captured leave ID; setup/catch/leave fault boundaries; captured item/current Result after menu;
pending Run and concurrent Stop/Start without new locks; exact host subscription/dispatcher callback timing.

### Task 74: Manual test session owner and characterization

Create `CDPIUI/ViewModels/BlockCheck2/BlockCheck2ManualTestSession.cs`,
`CDPIUI/ViewModels/BlockCheck2/IBlockCheck2ManualTestHost.cs`,
`CDPIUI/ViewModels/BlockCheck2/BlockCheck2ManualComponentTestTasks.cs`,
`CDPIUI.Tests/Presentation/BlockCheck2ManualTestSessionTests.cs`; modify Page.cs/.ManualTesting.cs/testcsproj.
Consumes host interface11members, Func<IConfigMakerTestTasks> resolveTasks, Func<string> componentId,
Action<Tuple<string,bool>> host handler. Produces IsRunning, Task ToggleFromButtonAsync()/ToggleAsync()/
StartAsync()/StopAsync(bool=false)/StopOnLeaveAsync(), void HandleStateUpdated(Tuple<string,bool>).
Private nested ManualTestHost handles real Page effects; public host keeps old entrypoints/getter.

- [x] Tests for all spec branch/trace/async/reentry/fault/ID/restore/queue contracts; absent-owner CS0246 RED.
- [x] Extract original policy/state, cold constructor and lazy adapters; focused/full GREEN, GUI0-0,
  source451/compiled452 no losses/+7 prior shapes, physical counts/diffcheck; scoped local commit.

### Task 75: Actual manual test composition

- [x] New immutable F copy from115; preserve23oldbootstraps with one exact old selection-helper bridge,
  add6groups=121. New19cuts/24bootstraps: controlledtasks receiver/currentID/logger only in
  ManualTesting; Page ctor itself remains exactpin, real private handler TargetPage/subscription,
  real dispatcher, captured/current inputs,
  pending/restore/leave/exit queue and partial faults; no real components/processes/native dialogs.
- [x] Independent prelaunch full inputs/cuts/bridge/artifact pins; explicit current-owner UI handoff/grant,
  121unique/first115/COMPLETE/exit0/noFAIL/empty streams/5binary+sources/source/input/output/6newnames/
  absence/priornames/process checks; release. Exact committed-security joint full suite/GUI.

### Task 76: Final integration review

- [x] Fresh review0d8ba6f..current plus original/newfive focus and closed branch context; mandatory
  RED/GREEN fix pass, explicit rulings/Minors; accurate state/private-reflection/adapter/IO boundaries.
  Refresh docs/pins/physical counts, local checkpoint, preserve archives and all other worktrees.

Task74 product2e70779:70focused/1947full/GUI0-0/39.26s/source451(600)compiled4520+7/normal670A,
host705→572(main305→308,manual186→50). Actor owns3flags, nestedhosteffects/currentResult, samePage
handler, FuncTasks receiver-before-ID withboundmanageradapter; originalpolicypreserved.
Test-only491692bc closes2reviewMinors:separateconcurrentgates+false/nullrunningbuttonStoppedtoast;
buttonmutant2FAIL→restoredsource72focused60ms/1949full10sGREEN, productreviewACCEPT0/0/0.
Native121 copy491built0-0/79.64s,1506repo1239project19cuts24bootstraps/oneexactoldselectionbridge/
5binary8sourcepins/asInvoker/noReparse/522priornames. PrelaunchREADYincludingextraSHA-pinnedreadback
runnerC09821A4/readback87A835C3/unique121-first115/golden354-260reports/6newnames/absence/normal670A.
Joint exact491+EE2161/2161/42s/GUI0-0/61.24s. Runtime121PASS+COMPLETE/exit0,PID69044
UTC17:46:43.3819843→17:46:47.3377306/121unique-first115/logsbyteequal/13actualpins/3inputs16-16-15/
2retainedreports354-260goldenSHA+literalcurrentfields/6newnames522→528/priornames/forbiddenabsence/
normal670Aunchanged/ownedPIDgone. ExplicitpipelineV27grant→V28canonicalrequestalias(sameRootowner)
→postchecks/RootALLUIpaused/idleack/requestcompletedfalse/V29releasedowner:null; peers notified.
Task75/76COMPLETE, final evidence/docsreviewACCEPT0Critical0Important0Minor. One documentary chronology
Minor corrected:1947full+GUI before receiver mutant, restore→70focused→source/metadata inventories;
later49172/1949. Independent1533jointfiles/166overlays/additivecsproj/pins/runtime/queue verified.
No Rootdesktop/CUA APIs/no redundant GUI rerun;126Ruling/17DeferredMinor retained.

### Continuation77–79: assignment actions

Spec: [assignment actions](../specs/2026-11-10-assignment-actions-design.md), base72c5554.
Inline executing-plans, autonomous authorization and prior global constraints retained. Review Focus:
captured selected row versus current Result after chooser; paths snapshot/identity and cancellation;
restore selected assignment after update reentry; exact duplicate menu updates; sync vs Task faults.

### Task 77: Assignment action owner and characterization

Create `CDPIUI/ViewModels/BlockCheck2/BlockCheck2AssignmentActions.cs`,
`CDPIUI/ViewModels/BlockCheck2/IBlockCheck2AssignmentActionsHost.cs`,
`CDPIUI/Helper/UserExperience/SelectedItemChoiceAction.cs`,
`CDPIUI.Tests/Presentation/BlockCheck2AssignmentActionsTests.cs`,
`CDPIUI.Tests/Presentation/SelectedItemChoiceActionTests.cs`.
Modify Page.cs/.Assignments.cs and testcsproj. Consumes internal host13members in spec. Produces
void AddSelectedStrategyForSite(), Task AddSelectedStrategyForListAsync(), void ShowResult(bool),
void MoveSelectedAssignment(int), RemoveSelectedAssignment(), ClearAssignments(),
ApplyManualIssueQuickFix(Func<bool>). Page entrypoints/private ShowAssignmentResult retained.
Neutral SelectedItemChoiceAction.ExecuteAsync<TItem,TChoice>(Func<TItem?> selected,
Func<TItem,IReadOnlyList<TChoice>> choices, Func<IReadOnlyList<TChoice>,Task<TChoice?>> choose,
Action<TItem,TChoice> apply) where both types:class; no BlockCheck2/WinUI dependency.

- [x] Characterization before owner: guards/traces/paths identity/pending/current Result/reentry/
  concurrent/cancel/fault timing/partial failures, absent-owner RED then original extraction GREEN.
- [x] Controlled selected-reread mutant RED→byte/SHA restoration→focused/full GREEN; normal GUI0-0,
  source/metadata contracts and sizes; scoped local checkpoint.

### Task 78: Actual assignment composition

- [x] Fresh immutable copy from121, preserve all24bootstraps/19cuts, add4groups=125/25bootstraps;
  real Page/Result/editor/localizer/privateadapter/preview, controlled chooser/view only, no new IO.
- [x] Fresh independent prelaunch READY including every oldcase byte/pin/source/binary/IO gate;
  coordinate explicit allUI lease;125unique/first121/COMPLETE/exit0/allpostchecks and release queue.
  Exact committed-security joint full suite/GUI, preserve previous source/binary/IO evidence.

### Task 79: Final integration review

- [x] Fresh review72c5554..current with original and new five Review Focus; fix evidenced defects via
  RED/GREEN, ledger rulings/Minors, current docs/API/runtime/scopes/pins; scoped local checkpoint.

Task77 productb5cce888/source61fd0e7(test-only): neutralhelper39lines4ports/domainowner80lines13hostmembers,
Page572→513/Assignments171→110/main308→310.47focused/1996full/GUI0-0/41.73s/source451(603)0loss/
compiled4520loss+7oldadds/normal24797CCA. Exactly-oncecoverageMinor fixedin61fd, duplicate-singleapply
mutantFAIL1→bytesSHArestore→47focused38ms/1996full10sGREEN/productre-reviewACCEPT0/0/0.
Joint61fd+EE2208/2208/41s/GUI0-0/59.36s; independent1538paths/172overlays/109additivecsprojitems exact.
Task78copy61fd0-0/71.78s/1512repo1242project/exact19cuts25bootstraps/23oldcasebytes/14actualpins,
prelaunchREADY. PID68696 UTC18:37:05.7871699→18:37:17.5133853:125unique/first121/COMPLETE/exit0/
noFAIL/empty streams/logsbyteequal/14pins/3inputs16-16-15/2reports354-260goldenSHA+literalfields/
522→528sixnewnames/priornames/absence/normal24797unchanged/PIDgone. ExplicitV31Rootgrant from
retrospective→postchecks+idleack/requestfalse/RootALLUIpaused→V32returnactivegrantretrospective;
bothpeersnotified/noRootdesktopCUA APIs. Tasks78/79COMPLETE, final evidence/docsreviewACCEPT0/0/0;
two documentarylocatorMinors closed. Independent125prefix/logs/14pins/3inputs2outputs/528recursive
names/absence/PID/queue/normalDLL/currentjointsource boundaries confirmed;130Ruling17Minor preserved.

### Continuation80–82: neutral two-phase transitions

Spec: [transition sequence](../specs/2026-11-10-transition-sequence-design.md), base9b65f3a.
Inline executing-plans/autonomous authorization/globalneutralcriterion. Ownership coordinator verified
no conflicting WIP/implementer in9worktrees; selected UtilityButtonControls only, not Store/backend.
Review Focus: inspect before apply vs delay after; token-read only if an actual delay is needed;
captured total vs live lists/inspect across await; old/new CTS identity cleanup; any-OCE swallowing
from body but not finalizer, no extra async wrapper between last apply and finalization.

### Task 80: Neutral transition mechanism and host forwarding

Create `CDPIUI/Helper/UserExperience/TwoPhaseTransitionSequence.cs`,
`CDPIUI.Tests/Presentation/TwoPhaseTransitionSequenceTests.cs`; modify
`CDPIUI/Controls/Universal/UtilityButtonControls.xaml.cs` and testcsproj.
Produces static Task RunAsync<TItem,TState> with two list inputs, inspect/apply/two delay selectors/
delay/borrowed CTS/onFinally exactspecports. Existing private3parameter Task method/Max/HasVisible kept;
Task forward avoids extra await; literal CTS finalizer callback stays in control, catch/finally execute
in the same generic async sequence. Implementation async attributes/stack frames are not public API.

- [x] Characterization and absent-helper RED before source; exact effects and async/fault/lazy-token/
  live-list/independent-call/borrowed-lifetime tests; original algorithm extraction, focused GREEN.
- [x] Mutation negativecontrol→byte/SHA restore→focused/full GREEN; normalGUI0-0/source/metadata
  inventory/physical counts/diff, scoped local checkpoint for immutable native/joint verification.

### Task 81: Actual UtilityButtonControls composition

- [x] New immutable Fcopy from125, preserve25oldbootstraps/19cuts andall24oldcasebytes, add4groups=129/
  26bootstraps. Realunloadedcontrol constructor/XAML/Buttons/Visibility/privateasync/Task.Delay;
  no Composition/navigation/settings/help/backend/desktop or new IO. UISettings readonly noted.
- [x] Independent prelaunch READY fullsource/pins/asInvoker/freshness/IOlimits; coordinate explicit UI
  grant,129unique/first125/COMPLETE/exit0/empty streams/allpostchecks/processgone/queue release.
  Exact committed-security joint fulltests/GUI, preserve all previous evidence and normal binary.

### Task 82: Final integration review

- [x] Fresh review9b65f3a..current with original and new five focuses; RED/GREEN fix pass, explicit
  rulings/Minors, exact source/API/runtime/joint/neutrality/visual limits; current docs/local checkpoint.

Task80 producta5a1b9b/helper50lines/control536→500/directTaskforward/noextraasyncboundary;
30focused51ms/2026full11s/GUI0-0/52.89s/source451(604)0loss/compiled4520loss+7oldadds/normal2F0B.
CS0103RED×2/inspect-afterapplymutantFAIL1→originalbytesSHA66FBrestored, productreview82ACCEPT0/0/0.
Coordinatorstandalone12+expected11negative/context-observerproof; exactjointA5+EE2238/2238/44s/
GUI0-0/85.38s,1540expectedpaths/1539Gitblobs/175overlay/110additivecsprojitems independentlyverified.
Task81 actual129Fcopy0-0/70.42s/1515repo1243project/exact19cuts26bootstraps/24oldcasebytes/
15actualpins/2prelaunchREADY. PriorV39→40coordinationguard stoppedbeforeeffects; thenexplicitV41Rootgrant,
PID81460 UTC19:40:40.6399817→19:40:46.0595732/129unique-first125/COMPLETE/exit0/noFAIL/streams0/
logsbyteequal/15pins/3inputs16-16-15/2reports354-260goldenSHA+literalfields/522→528recursive6newnames/
priornames/absence/normal2F0Bunchanged/PIDgone. Rootpostchecks/idleack/requestfalse/ALLUIpaused→
atomicV42releasedowner:null/bothpeersnotified/noRootdesktopCUA APIs. Tasks81/82COMPLETE,
finalevidence/docsreviewACCEPT0Critical0Important0Minor;1515fixture/15pins/129prefix/IO/528names/PID/
queue/604GUI/joint1540paths110XMLitems175overlays independentlyverified.132Ruling17Minor retained;
allpreviousarchivespreserved/no rerun/deletion, product/source unchangedafterA5.

### Continuation83–85: neutral postorder menu pruning

Issue209; spec [menu pruning](../specs/2026-11-10-menu-pruning-design.md). Basea85efec4,
productA5. Issue/Project actual fields confirmed before implementation. Other executor owns208
ConfigMaker/TextChangeRelay/CreateNewConfig/App; Root owns MenuBarController and new helper only.
Review Focus: reverse live index traversal; retention after descendants; registry then physical
then logical mutation; exception identity/partial effects; reentry without snapshot or repair.

### Task 83: Neutral pruning mechanism and host policy

Create Helper/UserExperience/PostOrderTreePruner.cs and Presentation/PostOrderTreePrunerTests.cs;
modify Controls/MenuBar/MenuBarController.cs and additive testcsproj. One System-only generic
Prune(IList<T>, children, retain, remove) entry point as specified; no new partial/domain owner.

- [x] Characterization first, absent-helper RED; extract exact algorithm and host removal policy.
  Expected focused GREEN with observable ordering, mutation and failure contracts.
- [x] Controlled eager-retention mutant fails; exact byte/SHA restoration; focused and full tests,
  normal Release/x64 GUI build0-0, source/metadata inventory and source checkpoint. No native claim yet.

### Task 84: Actual menu composition and security joint

- [x] Fresh immutable copy from129 with four new groups=133/27bootstraps, same19cuts and25old
  case partials byte-identical; standalone unactivated actual menu/controller only, no newIO.
  Expected independent prelaunch READY with source/binary/asInvoker/freshness/IO gates.
- [x] Explicit allUI grant, one-shot133unique/first129/COMPLETE/exit0/allpostchecks/processgone,
  idleack and queue handoff. Exact committed-security joint fulltests and GUI0-0, source composition.

### Task 85: Independent review and local acceptance

- [x] Fresh final review83–85 with original and new five focuses; verified corrective pass if needed,
  accurate current docs/rulings/limits/checkpoint. Expected no unaddressed Critical/Important findings.
- [x] All209 local criteria evidenced; acceptance comment, immediate completed+ProjectDone and actual
  metadata readback. Integration14/installed15 remain open; continue only into an unowned next task.

Task83–85 COMPLETE: product174d1cb/docs-evidencee7436d0;20focused/2046full/GUI0-0/source4516050/meta4520+7;
actual133first129/exit0/allpostchecks/18pins/PID68204gone/explicit48→idle-requestfalse→49peer208;
exactEEjoint2258/GUI0-0/1542paths111XMLadds; parallelb577+174+EE2287/GUI0-0/1544paths112dedupadds.
FreshfinalreviewACCEPT0Critical0Important0Minor,133Ruling17Minor preserved. Sourcecontroller519→502
correctedfrom501transcriptionwithoutsourcechange.209acceptcomment6779106275→APIclosedcompleted/6criteria[x]
→Project52Done/readbackafterreload/finalcomment6779317178; exact3labels/Lumens/G2/Low/P2Architecture.
Parents14/15LIVEopen, no push/merge/releaseclaim. Next211iscreatedbutimplementationgatedonactualProjectintake.

### Continuation86–88: Core variable read-model projection

Issue211; spec [configuration projection](../specs/2026-11-11-configuration-variable-projection-design.md).
Basec089265/source174. Actual55ProjectInprogress/P2/Architecture/Low/G2/Lumens confirmed;
209completed+Done prerequisite satisfied. Root owns ConfigurationService/new helper/tests only;
peer212 Navigation/LoadVars/named-loader and ConfigMaker/Create/App stay reserved.
Review Focus: source dictionary enumeration order/stored spelling and fresh ownership; first OrdinalIgnoreCase metadata match;
Ordinal stable distinct options; lazy null/first-match faults and current metadata per entry;
joint snapshots include the changed Core source rather than earlier GUI-only overlays.

### Task 86: Characterize the facade and extract the model adapter

Create Core Configuration/Helpers/ConfigurationVariableProjection.cs and
CDPIUI.Tests/ConfigurationVariableProjectionTests.cs; modify only ConfigurationService.cs.
Produces internal static ToVariableItems(Dictionary<string,bool>) and ToCommaVariableItems(ConfigItem)
with existing models. No testcsproj source link, visibility widening, helper-existence test or new API.

- [x] Actual public static/instance facade characterization on owned models first. Expected original
  GREEN, then original first-match mutant RED; restore exact bytes/SHA and confirm focused GREEN.
- [x] Extract original loops/used private forward/host guards; extracted-helper mutant must cause RED
  through actual facade, then exact byte restoration and final focused GREEN. No IO/native UI involved.

### Task 87: Core contracts and parallel regression evidence

- [x] Capture original Core assembly/source; focused/full tests GREEN, normal GUI0-0; before/after Core
  source/compiled contracts0loss and unchanged605GUI sources/GUI metadata. Source checkpoint for joint.
- [x] Fresh exact committed-Security joint with Core-inclusive overlay, then committed peer composition
  when available; tests/GUI0-0 and full Git/blob/XML/extra-input proof. Preserve old snapshots/logs.

### Task 88: Independent review and local acceptance

- [x] Fresh independent review86–88 with original/newfocuses, verified corrective pass if needed;
  current evidence/docs/limits, no unaddressed Critical/Important findings and local checkpoint.
- [x] All211criteria evidenced; acceptance record, immediatecompleted+ProjectDone and actualmetadata
  readback. Parents14/15 remain open; actualCoreWindows facade proof is separate from old WinUI133.

Task86–88 COMPLETE: source00f39b1a9be70cb97cebd7290b5f24dac6a5ab47/docsf573f4f;
30actualCorefacade/2076full/GUI0-0(70.77s),Core88source103→104/compiled88/0changes;
605GUIsourcesunchanged174/GUIcompiled4520loss+7old. CoreinclusiveEE2288/GUI0-0/1544paths111XML;
peer9a9+Root+EE2338/GUI0-0/1548paths113XML. Bothbefore/afterproofs independentlyreconstructed.
Original/helpermutants3FAILeach→exactrestoreGREEN, typedSourcepublicAPI/noWinUInewruntimeclaim.
DocorderMinorcorrectedviaRuling135;135Ruling17oldDeferredMinor. Finalreview0CriticalImportantMinor,
accept6788006950→API211closedcompleted6criteria[x]→Project64actualDone/P2ArchitectureLowG2Lumens2labels
readbackafterreload/final6788179295. Parent14/15LIVEopen, no push/merge/release.

### Continuation89–91: Diagnostic highlighting updates

Issue215; [design](../specs/2026-11-11-diagnostic-highlighting-design.md), base80c1ec2/source00f39.
Actual69ProjectInprogress/P2ArchitectureMediumG2Lumens/3labels readback beforeimplementation;
211Done64 prerequisite verified. Root onlyControl.Editor/newSystemupdater/tests; peerVM/Create/Appreserved.
Review Focus: separate live passes/Source reads; raw-key collisions and current post-collection comparison;
commit before callback/sticky faults/reentry; literal UI receiver/constructor/Design order; transitive
Search/ConsoleFont preference isolation before actualControl construction.

### Task 89: System-only highlighting update state and host delegation

Create Helper/UserExperience/DiagnosticHighlightingUpdater.cs and Presentation/DiagnosticHighlightingUpdaterTests.cs;
modify onlyControl.Editor.cs and additive testcsproj. One signature field/Reset/sevenport Update<T> exactspec.

- [x] Characterize with plain nodes/traces before helper, missing-helper RED then original extraction GREEN.
  Expected two current reads/error-first/lazy tokens/raw data/collection-reentry and exact fault boundaries.
- [x] Controlled commit-after-callback mutant RED with bounded reentry, exact byte/SHA restoration;
  focused/full GREEN, normal GUI0-0/source+compiled contracts, source checkpoint. No publicAPI/VM changes.

### Task 90: Actual control composition and parallel joints

- [x] Fresh archive from new committed source and accepted menu133casebase, four new groups=137/28bootstraps/
  26oldcasebytes. Old19cuts plus exactly Search/ConsoleFont preference endpoints=21, typedinmemorycallbacks/
  fixedvalues, no product preference changes or newIO. Expected fullsource/pin/asInvoker/freshness proof.
- [x] Independent prelaunch READY/exclusive explicit grant;137unique/first133/COMPLETE/exit0/streams0/
  allpins/3inputs2goldenreports/6newnames/absence/processgone, idle/handoff. Exact committed-Security and
  corrected availablepeer joint tests/GUI/source+XML proof, preserve original and rejected artifacts.

### Task 91: Independent review and local acceptance

- [x] Fresh review89–91 incl all original/new focuses; verified corrective pass if needed, current docs/
  rulings/limits/localcheckpoint, no unaddressed Critical/Important findings.
- [x] All215criteria evidenced, acceptance record/immediatecompleted+ProjectDone/actualreadback;
  parent14/15 remain open, real visuals/startup/installed integration not inferred from bounded137.

Task89–91 COMPLETE local: producted390cd416b58ea404f04286f7eace5a66e4320a/docstechnicalcee65ac;
32focused/2108full/GUI0-0/source4516060loss/compiled4520loss+7old, helpermutant4RED→exactrestoreGREEN.
ExactEEjoint2320 and correctedpeerf706+EE2398/GUI0-0/fullsource+XML independentlyverified.
R2actual137unique/first133/COMPLETE/exit0/all23pins/1531Git21cuts28bootstrap26oldbytes/IO/PID79052gone/
explicit83→idle/requestfalse→84release. OriginalcompiledV1preservedunlaunched; Count/import/attribution
corrections+fonttiming/statusdocMinors retained. FinalfreshACCEPT0unresolvedC/I/M;141Ruling18DeferredMinor.
215accept6789505833→APIclosedcompleted6[x]→actualProject85Done/P2ArchitectureMediumG2Lumens3labels
readbackafterreload/86released. Parents14/15open; no push/merge/releaseorunionnativeclaim.
