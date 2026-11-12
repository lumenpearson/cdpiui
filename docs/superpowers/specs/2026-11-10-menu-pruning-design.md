# Postorder pruning for the menu controller

Issue: https://github.com/lumenpearson/cdpiui/issues/209. Local stage only; integration and
installed-product acceptance remain with #14 and #15. Execution owner is thread01a1bc96 on
dev/audit-class-decomposition. Base a85efec4 contains the exact86417 task policy; product/tests
are still A5. Actual Project fields were read back: In progress/P2/Architecture/Low, G2,
lumenpearson. ConfigMaker/CreateNewConfig/App and TextChangeRelay belong to the other executor.

Extract only recursive pruning from the519-line MenuBarController. A small System-only
PostOrderTreePruner has one entry point:

`Prune<TNode>(IList<TNode> nodes, Func<TNode,IList<TNode>> children,
Func<TNode,bool> retain, Action<TNode> remove)`.

For each list, capture its starting last index and visit indices backwards. Read the current
node at that index, obtain its child list once, and recursively prune it. Then evaluate the
current retain predicate. If false, invoke remove before RemoveAt(index) on the same list.
Do not copy lists, precompute retention, enumerate forward, retry, catch faults, repair indices,
add cancellation, transactions, cycle guards or null guards. Reentry and list mutation retain
the original algorithm's partial effects and index failures. Null callbacks need not be read
for an empty list. Child-list identity is stable in MenuEntry (readonly get-only List).

The host keeps strong/weak/retained-child policy in its predicate. The host removal callback
keeps registry removal before physical WinUI removal, including the ignored physical Remove
return value. RemoveAt follows only a successful callback. Other scope, integration, ordering,
identifier, navigation, session and lifetime code remains unchanged. The old private recursive
PruneEntry implementation moves to the utility; no recorded public API uses that private method,
and a source search found no fixture or production reflection binding to it. Do not add an
unused private forwarding method. There is one current consumer, with no invented reuse claim.

Managed characterization covers reverse roots/children, postorder retention, strong/weak claims,
retained descendants, parent state changed from a child callback, collection identity, failure
order and exception identity, partial removal, live insertion and reentry. A controlled mutation
that evaluates retention before pruning children must fail; restore original source bytes in a
finally block and verify SHA before final focused/full tests. Build normal GUI and compare the
existing source and compiled contract inventories; these are bounded checks, not complete ABI proof.

Native continuation fixes four new groups,133 total, based on accepted actual129. Use standalone
unactivated real MenuBar/Frame/controller/attached lifetime and identifier properties. Verify weak
scope removal, strong or claimed descendants, branch outcomes and actual physical/logical/registry
composition. No Window activation, navigation, backend, desktop input or new IO is authorized by
this fixture. Existing19 cuts and26 bootstraps stay; one bootstrap adds menu cases (27 total).
All25 previous case partials stay byte-identical. Preserve old129 ordered PASS texts and all
binary/source/input/output/absence/process gates. A separate fresh prelaunch review and explicit
exclusive UI grant are prerequisites to launching. Existing accepted129 is historical baseline,
not new133 evidence. Retain failures and archives; never rerun a one-shot output location.

Verify an isolated joint source with exact committed securityEE. Obtain fresh independent final
review of this local continuation, update status/map/decision evidence, and add case-specific issue
acceptance. Close #209 completed plus Project Done only after every local criterion is satisfied,
with actual readback. Do not close the broad integration/release parents or claim visual/full
startup/installed behavior from the bounded menu fixture.
