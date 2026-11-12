# Configuration variable read-model projection

Local issue211, basec089265/source174d1cb. The historical audit lists ConfigurationService among
large classes; it is443lines at this source. This pass extracts only its boolean/choice read-model
loops. Actual Project intake55 was read after reload: In progress/P2/Architecture/Low/G2/lumenpearson,
type:refactor+area:architecture. Dependency209 is closedcompleted/actualDone. Root owns this Core
service/new helper/tests; peer212 owns CreateNewConfig.Navigation/LoadVars and its named loader,
peer208 owns its accepted relay; ConfigMaker/Create/App remain outside this implementation.

Add a small internal stateless ConfigurationVariableProjection under Configuration/Helpers:
`List<VariableItem> ToVariableItems(Dictionary<string,bool> jparams)` and
`List<CommaVariableItem> ToCommaVariableItems(ConfigItem configItem)`.
This is a coherent adapter to existing domain models, not a generic Select framework or a new
large service. It uses no WinUI/backend/settings/DB/filesystem, state, interfaces or new packages.
No public API is added. Preserve private ToVariableItems as a used forwarding method for both
public GetVariables overloads; GetCommaVariables keeps its original FindItem and null guard.

Boolean projection enumerates the original dictionary, in the order supplied by the source dictionary and with stored spelling;
each call allocates a new list and new models, setting name then value, leaving variable_name null.
Static GetVariables(null) and a null jparams still throw as before; instance missing item/dictionary
still returns a fresh empty list. FindItem's ordinal file/pack lookup, locks and all mutation methods
remain unchanged. Returned model mutations do not update the underlying dictionary.

Comma projection receives the captured ConfigItem itself. The dictionary expression is evaluated
at the original foreach point; metadata is read from its current availableCommaVarsValues at each
iteration. Do not pass an earlier metadata snapshot, build a lookup, cache results or normalize keys.
FirstOrDefault uses OrdinalIgnoreCase on VarName; duplicate matches keep the first metadata row.
Options use Distinct(StringComparer.Ordinal).ToList(), preserving first occurrence/case variants,
then initializer order name/comment/value/values. Keep the raw current value even if absent from
options, null or empty. Missing metadata has a null comment/empty list; null Values also gives a
fresh empty list. Each returned options list is independent even when two keys share metadata.
Runtime-null metadata entries retain lazy first-match failure/short-circuit boundaries. Empty
dictionaries never inspect malformed metadata. Do not catch/repair errors or change source models
(including CurrentValueIndex). The helper's null-forgiving annotation adds no runtime guard.

Tests invoke the actual public Core facade with owned ConfigItem/AvailableVarValues and SetConfigItem,
without Init/Save or private reflection. Keep service guards and observable result ownership/order/
comparers/nulls. Characterization is expected GREEN on the original implementation. A deliberate
original first-match mutation must fail before extraction; restore exact bytes/SHA, then extract.
Repeat the mutation against the extracted helper: a RED proves the facade actually executes it,
then restore exact bytes and run focused/full GREEN. No helper-existence test, public test seam,
InternalsVisibleTo or duplicate source-linked Core helper is needed.

Capture pre-change Core source and actual assembly before extraction. Compare after Core source and
compiled public shapes with the existing bounded analyzers; do not claim complete ABI compatibility.
All605 GUI sources must remain equal to174. Build normal Release/x64 GUI, retain normal binaries and
compare its recorded public metadata. Native WinUI launch is not relevant to these pure Core loops:
actual public facade execution on Windows tests the changed behavior and assembly composition;
UI types/bindings/startup are untouched. Historical133 belongs to174, not new-Core execution proof.
Any later scope change into UI/native requires a revised contract and exclusive grant.

Joint snapshots MUST overlay CDPIUI.Core as well as CDPIUI and CDPIUI.Tests. Earlier GUI-only
snapshot builders would omit this change and could pass behavior tests against the old implementation.
Use exact committed SecurityEE and committed peer relay/navigation when available, full source/blob/
XML reconstruction, no deletions or WIP, one-shot fresh F directories. Verify every expected file and
only the empty generated Secret extra outside build outputs. Keep original failed attempts and archives.

Obtain fresh independent review of this continuation and current evidence, update plan/status/map and
decision ledger. This accepts a LOCAL source/test/build result only. Publish/merge, final-PR-HEAD CI,
installed product and broad native acceptance remain with14/15. After scoped acceptance, immediately
record issue211completed plus actualProjectDone/readback in the same working turn. No user confirmation
is needed under the continuing authorization; all Root UI calls remain paused.
