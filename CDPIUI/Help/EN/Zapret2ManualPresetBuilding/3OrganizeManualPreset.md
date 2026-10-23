# Scopes, sequence and warnings

In [Preset builder], each row shows its scope, connection type and selected strategy. The preview below the list updates after each change.

### Manage the list

- To move the selected row up or down, use the arrow buttons.
- To delete the selected strategy, select [Remove].
- To clear the manual preset, select [Remove all].

If a scope has one strategy, this strategy is the primary strategy. If a scope has more than one compatible strategy, the first strategy is primary. The next strategies are fallbacks. Thus, the sequence of rows in one scope is important.

Do not add fallbacks only to have more strategies. Verify each fallback for its full scope.

### Site scope and list scope

A strategy for one site is more specific than a strategy for a full list. If scopes overlap, the builder puts the more specific profile before the general profile. It also shows a warning. Examine the result.

[Accept order] only confirms that you examined the sequence. It does not move rows or change a strategy. It does not confirm that the strategy operates. The warning can show again after a change in the builder.

### Warnings

| Warning | Meaning and action |
| ------- | ------------------ |
| **No test data** | Test the strategy again for the selected scope. |
| **Threshold not passed** | The result of the strategy is not sufficiently reliable. |
| **Partial scope coverage** | Some sites or connection types failed. Select a different strategy, or make the scope smaller (one site). |
| **Overlapping scopes** | Examine which profile the component applies first. |
| **Strategies cannot be combined** | Keep one strategy in this scope, or select a compatible strategy. |

An error can prevent the builder from making a usable preset text. A warning does not stop you, but you must examine it manually.

### For advanced users

The window can show these technical codes:

| Code | Meaning |
| ---- | ------- |
| `MANUAL_STRATEGY_UNTESTED` | The strategy has no results for the selected scope. |
| `MANUAL_STRATEGY_UNPROVEN` | The strategy did not reach the necessary threshold. |
| `MANUAL_SCOPE_PARTIAL` | Only a part of the scope passed. |
| `MANUAL_SCOPE_OVERLAP` | Two scopes contain the same connections. |
| `MANUAL_CIRCULAR_UNSUPPORTED` | The selected strategies cannot operate together as primary and fallback strategies. |
| `MANUAL_STRATEGY_INAPPLICABLE` | The strategy does not support all connections in the selected scope. |

Next: [Test and complete the manual preset](cdpiui://Help/Zapret2ManualPresetBuilding/4FinishManualPreset/).
